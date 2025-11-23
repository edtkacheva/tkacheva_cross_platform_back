using Microsoft.EntityFrameworkCore;
using tkacheva_lr2.Data;
using tkacheva_lr2.Models;

namespace tkacheva_lr2.Services
{
    public class UserService
    {
        private readonly ApplicationDbContext _context;

        public UserService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<AppUser>> GetAllUsersAsync()
        {
            return await _context.AppUsers.AsNoTracking().ToListAsync();
        }

        public async Task<AppUser?> GetUserByNameAsync(string username)
        {
            return await _context.AppUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserName.ToLower() == username.ToLower());
        }

        public async Task<AppUser> CreateUserAsync(AppUser user)
        {
            if (!user.IsPasswordStrong())
                throw new ArgumentException("Password is too weak (min 6 chars).");

            if (await UserExistsAsync(user.UserName))
                throw new InvalidOperationException("UserName already exists.");

            _context.AppUsers.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<AppUser?> UpdateUserAsync(string username, AppUser data)
        {
            var user = await _context.AppUsers
                .FirstOrDefaultAsync(u => u.UserName.ToLower() == username.ToLower());

            if (user == null) return null;

            if (!string.IsNullOrWhiteSpace(data.UserName))
            {
                var exists = await _context.AppUsers.AnyAsync(u =>
                    u.UserName.ToLower() == data.UserName.ToLower() && u.Id != user.Id);

                if (exists)
                    throw new InvalidOperationException("New username already taken.");

                user.UserName = data.UserName;
            }

            if (!string.IsNullOrWhiteSpace(data.Password))
                user.Password = data.Password;

            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<bool> DeleteUserAsync(string username)
        {
            var user = await _context.AppUsers
                .FirstOrDefaultAsync(u => u.UserName.ToLower() == username.ToLower());

            if (user == null) return false;

            _context.AppUsers.Remove(user);
            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<bool> UserExistsAsync(string username)
        {
            return await _context.AppUsers.AnyAsync(u =>
                u.UserName.ToLower() == username.ToLower());
        }

        public async Task<bool> SubscribeAsync(string username, string channelName)
        {
            var user = await _context.AppUsers
                .Include(u => u.SubscribedChannels)
                .FirstOrDefaultAsync(u => u.UserName.ToLower() == username.ToLower());

            if (user == null)
                return false;

            var channel = await _context.RSSChannels
                .FirstOrDefaultAsync(c => c.Name.ToLower() == channelName.ToLower());

            if (channel == null)
                return false;

            if (!user.SubscribedChannels.Contains(channel))
            {
                user.SubscribedChannels.Add(channel);
                await _context.SaveChangesAsync();
            }

            return true;
        }

        public async Task<bool> UnsubscribeAsync(string username, string channelName)
        {
            var user = await _context.AppUsers
                .Include(u => u.SubscribedChannels)
                .FirstOrDefaultAsync(u => u.UserName.ToLower() == username.ToLower());

            if (user == null)
                return false;

            var channel = user.SubscribedChannels
                .FirstOrDefault(c => c.Name.ToLower() == channelName.ToLower());

            if (channel == null)
                return false;

            user.SubscribedChannels.Remove(channel);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<List<RSSChannel>> GetSubscriptionsAsync(string username)
        {
            var user = await _context.AppUsers
                .Include(u => u.SubscribedChannels)
                .FirstOrDefaultAsync(u => u.UserName.ToLower() == username.ToLower());

            return user?.SubscribedChannels ?? new List<RSSChannel>();
        }

    }
}