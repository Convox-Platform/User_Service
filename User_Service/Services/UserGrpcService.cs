using Grpc.Core;
using System.Data.Common;
using System.Globalization;
using User_Service.Models;

using Dapper;
using Microsoft.AspNetCore.Authorization;
using Npgsql;


namespace User_Service.Services;

public class UserGrpcService: UserService.UserServiceBase
{
    private readonly DbConnection _db;
    private readonly RabbitMqPublisher _publisher;

    public UserGrpcService(DbConnection db, RabbitMqPublisher publisher)
    {
        _db = db;
        _publisher = publisher;
    }

    public override async Task<UserProfileResponse> CreateUserProfile(
        CreateUserProfileRequest request,
        ServerCallContext context)
    {
        

        var profile = new UserProfile
        {
            Id = request.UserId,
            Username = NormalizeUsername(request.Username),
            DisplayName = request.DisplayName,
            Img = request.Img,
            Status = "new user",
            StatusUpdatedAt = DateTime.UtcNow
        };

        string sql = "INSERT INTO UserProfiles (Id, Username, DisplayName, Img, Status, StatusUpdatedAt) VALUES (@Id, @Username, @DisplayName, @Img, @Status, @StatusUpdatedAt)";

        await SaveWithAvailableUsernameAsync(profile, sql);

        var message = new UserProfileRabbitMqEvent(Id: profile.Id, Username: profile.Username, DisplayName: profile.DisplayName, Img: profile.Img);
        await _publisher.PublisAsync(queueName:"user.profile.created", message,context.CancellationToken);

        return ToResponse(profile);
    }

    [Authorize]
    public override async Task<UserProfileResponse> GetUserProfile(
        GetUserProfileRequest request,
        ServerCallContext context)
    {
        var sql = "SELECT * FROM UserProfiles WHERE Id = @Id";

        var profile = await _db.QueryFirstOrDefaultAsync<UserProfile>(sql, new { Id = request.UserId });

        if (profile == null)
            throw new RpcException(new Status(StatusCode.NotFound, "User profile not found."));

        return ToResponse(profile);
    }
    [Authorize]
    public override async Task<UserProfileResponse> UpdateUserProfile(
        UpdateUserProfileRequest request,
        ServerCallContext context)
    {
        var sql = "SELECT * FROM UserProfiles WHERE Id = @Id";


        var profile = await _db.QueryFirstOrDefaultAsync<UserProfile>(sql, new { Id = request.UserId });

        if (profile == null)
            throw new RpcException(new Status(StatusCode.NotFound, "User profile not found."));

        if (!string.IsNullOrWhiteSpace(request.Username))
            profile.Username = NormalizeUsername(request.Username);

        if (!string.IsNullOrWhiteSpace(request.DisplayName))
            profile.DisplayName = request.DisplayName;

        profile.Description = request.Description;
        profile.Img = request.Img;
        profile.Status = request.Status;
        profile.StatusUpdatedAt = DateTime.UtcNow;

        profile.BirthDate = ParseBirthDate(request.BirthDate);

        var updateSql = "UPDATE UserProfiles SET Username = @Username, DisplayName = @DisplayName, " +
            "Description = @Description, Img = @Img, BirthDate = @BirthDate, Status = @Status, StatusUpdatedAt = @StatusUpdatedAt " +
            "WHERE Id = @Id";

        await SaveWithAvailableUsernameAsync(profile, updateSql);

        var message = new UserProfileRabbitMqEvent(Id: profile.Id, Username: profile.Username, DisplayName: profile.DisplayName, Img: profile.Img);
        await _publisher.PublisAsync(queueName: "user.profile.updated", message, context.CancellationToken);

        return ToResponse(profile);
    }
    [Authorize]
    public override async Task<SearchUsersResponse> SearchUsers(
        SearchUsersRequest request,
        ServerCallContext context)
    {
        var limit = request.Limit <= 0 ? 20 : request.Limit;

        var sql = @"
        SELECT * 
        FROM UserProfiles
        WHERE Username ILIKE CONCAT('%', @query, '%')
           OR DisplayName ILIKE CONCAT('%', @query, '%')
        LIMIT @limit;"; 

        var users = await _db.QueryAsync<UserProfile>(sql, new { query = request.Query, limit = limit });


        var response = new SearchUsersResponse();
        response.Users.AddRange(users.Select(ToResponse));

        return response;
    }

    

    private static UserProfileResponse ToResponse(UserProfile profile)
    {
        return new UserProfileResponse
        {
            Id = profile.Id,
            Username = profile.Username,
            DisplayName = profile.DisplayName,
            Description = profile.Description ?? "",
            Img = profile.Img ?? "",
            BirthDate = profile.BirthDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
            Status = profile.Status ?? "",
            StatusUpdatedAt = profile.StatusUpdatedAt.HasValue
                ? new DateTimeOffset(profile.StatusUpdatedAt.Value).ToUnixTimeSeconds()
                : 0
        };
    }

    private static DateOnly? ParseBirthDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateOnly.TryParseExact(
                value.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var birthDate))
        {
            return birthDate;
        }

        throw new RpcException(new Status(
            StatusCode.InvalidArgument,
            "Birth date must use the yyyy-MM-dd format."));
    }

    private static string NormalizeUsername(string value)
    {
        var username = value?.Trim() ?? string.Empty;
        if (username.Length is < 1 or > 100)
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "Username must be between 1 and 100 characters."));
        }

        return username;
    }

    private static bool IsUsernameConflict(PostgresException exception) =>
        exception.SqlState == PostgresErrorCodes.UniqueViolation &&
        exception.ConstraintName == "ux_userprofiles_username_normalized";

    private async Task SaveWithAvailableUsernameAsync(UserProfile profile, string sql)
    {
        var requestedUsername = profile.Username;

        for (var suffix = 0; suffix < 10_000; suffix++)
        {
            profile.Username = BuildUsernameCandidate(requestedUsername, suffix);

            try
            {
                await _db.ExecuteAsync(sql, profile);
                return;
            }
            catch (PostgresException exception) when (IsUsernameConflict(exception))
            {
                // A concurrent request may claim a candidate between attempts.
            }
        }

        throw new RpcException(new Status(
            StatusCode.ResourceExhausted,
            "Could not allocate an available username."));
    }

    private static string BuildUsernameCandidate(string requestedUsername, int suffix)
    {
        if (suffix == 0)
        {
            return requestedUsername;
        }

        var digits = suffix.ToString(CultureInfo.InvariantCulture);
        return requestedUsername[..Math.Min(requestedUsername.Length, 100 - digits.Length)] + digits;
    }
}
