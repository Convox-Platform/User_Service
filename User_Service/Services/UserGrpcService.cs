using Grpc.Core;
using System.Data.Common;
using User_Service.Models;

using Dapper;
using Microsoft.AspNetCore.Authorization;


namespace User_Service.Services;

public class UserGrpcService: UserService.UserServiceBase
{
    private readonly DbConnection _db;

    public UserGrpcService(DbConnection db)
    {
        _db = db;
    }

    public override async Task<UserProfileResponse> CreateUserProfile(
        CreateUserProfileRequest request,
        ServerCallContext context)
    {
        

        var profile = new UserProfile
        {
            Id = request.UserId,
            Username = request.Username,
            DisplayName = request.DisplayName,
            Img = request.Img,
            Status = "new user",
            StatusUpdatedAt = DateTime.UtcNow
        };

        string sql = "INSERT INTO UserProfiles (Id, Username, DisplayName, Img, Status, StatusUpdatedAt) VALUES (@Id, @Username, @DisplayName, @Img, @Status, @StatusUpdatedAt)";

        await _db.ExecuteAsync(sql, profile);

        

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
            profile.Username = request.Username;

        if (!string.IsNullOrWhiteSpace(request.DisplayName))
            profile.DisplayName = request.DisplayName;

        profile.Description = request.Description;
        profile.Img = request.Img;
        profile.Status = request.Status;
        profile.StatusUpdatedAt = DateTime.UtcNow;

        if (DateTime.TryParse(request.BirthDate, out var birthDate))
            profile.BirthDate = birthDate;

        var updateSql = "UPDATE UserProfiles SET Username = @Username, DisplayName = @DisplayName, " +
            "Description = @Description, Img = @Img, BirthDate = @BirthDate, Status = @Status, StatusUpdatedAt = @StatusUpdatedAt " +
            "WHERE Id = @Id";

        await _db.ExecuteAsync(updateSql, profile);

        return ToResponse(profile);
    }
    [Authorize]
    public override async Task<SearchUsersResponse> SearchUsers(
        SearchUsersRequest request,
        ServerCallContext context)
    {
        var limit = request.Limit <= 0 ? 20 : request.Limit;

        var sql = "SELECT TOP (@limit) *\r\nFROM UserProfiles\r\nWHERE LOWER(Username) LIKE '%' + LOWER(@query) + '%'\r\n   OR LOWER(DisplayName) LIKE '%' + LOWER(@query) + '%';";

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
            BirthDate = profile.BirthDate?.ToString("yyyy-MM-dd") ?? "",
            Status = profile.Status ?? "",
            StatusUpdatedAt = profile.StatusUpdatedAt.HasValue
                ? new DateTimeOffset(profile.StatusUpdatedAt.Value).ToUnixTimeSeconds()
                : 0
        };
    }
}