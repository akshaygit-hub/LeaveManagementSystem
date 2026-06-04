using Shared.Models;

namespace AuthService.Repositories;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByIdAsync(Guid id);
    Task<List<User>> GetTeamMembersAsync(Guid managerId);
    Task<IEnumerable<User>> GetAllAsync();
}
