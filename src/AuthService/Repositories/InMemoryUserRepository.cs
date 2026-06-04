using Shared.Models;

namespace AuthService.Repositories;

public class InMemoryUserRepository : IUserRepository
{
    private static readonly List<User> _users = new();
    private static bool _seeded = false;
    private static readonly object _lock = new();

    public InMemoryUserRepository()
    {
        lock (_lock)
        {
            if (!_seeded)
            {
                SeedData();
                _seeded = true;
            }
        }
    }

    private static void SeedData()
    {
        _users.AddRange(new[]
        {
            new User
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Username = "manager1",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Manager@123"),
                FullName = "John Manager",
                Email = "john.manager@company.com",
                Role = UserRole.Manager,
                ManagerId = null
            },
            new User
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Username = "manager2",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Manager@123"),
                FullName = "Jane Manager",
                Email = "jane.manager@company.com",
                Role = UserRole.Manager,
                ManagerId = null
            },
            new User
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Username = "employee1",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Employee@123"),
                FullName = "Alice Employee",
                Email = "alice.employee@company.com",
                Role = UserRole.Employee,
                ManagerId = Guid.Parse("11111111-1111-1111-1111-111111111111")
            },
            new User
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Username = "employee2",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Employee@123"),
                FullName = "Bob Employee",
                Email = "bob.employee@company.com",
                Role = UserRole.Employee,
                ManagerId = Guid.Parse("11111111-1111-1111-1111-111111111111")
            },
            new User
            {
                Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                Username = "employee3",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Employee@123"),
                FullName = "Charlie Employee",
                Email = "charlie.employee@company.com",
                Role = UserRole.Employee,
                ManagerId = Guid.Parse("22222222-2222-2222-2222-222222222222")
            }
        });
    }

    public Task<User?> GetByUsernameAsync(string username)
    {
        var user = _users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(user);
    }

    public Task<User?> GetByIdAsync(Guid id)
    {
        var user = _users.FirstOrDefault(u => u.Id == id);
        return Task.FromResult(user);
    }

    public Task<List<User>> GetTeamMembersAsync(Guid managerId)
    {
        var members = _users.Where(u => u.ManagerId == managerId).ToList();
        return Task.FromResult(members);
    }

    public Task<IEnumerable<User>> GetAllAsync()
    {
        return Task.FromResult<IEnumerable<User>>(_users.ToList());
    }
}
