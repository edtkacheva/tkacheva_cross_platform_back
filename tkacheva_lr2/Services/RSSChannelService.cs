using Microsoft.EntityFrameworkCore;
using tkacheva_lr2.Data;
using tkacheva_lr2.Models;

namespace tkacheva_lr2.Services
{
    public class RSSChannelService
    {
        private readonly ApplicationDbContext _context;

        public RSSChannelService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<RSSChannel>> GetAllChannelsAsync()
        {
            return await _context.RSSChannels
                .Include(c => c.Articles)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<RSSChannel?> GetChannelByNameAsync(string name)
        {
            return await _context.RSSChannels
                .Include(c => c.Articles)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower());
        }

        public async Task<RSSChannel> CreateChannelAsync(RSSChannel channel)
        {
            if (await ChannelExistsAsync(channel.Name))
                throw new InvalidOperationException("Channel name already exists.");

            _context.RSSChannels.Add(channel);
            await _context.SaveChangesAsync();
            return channel;
        }

        public async Task<RSSChannel?> UpdateChannelAsync(string name, RSSChannel updated)
        {
            var channel = await _context.RSSChannels
                .FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower());

            if (channel == null) return null;

            if (!string.IsNullOrWhiteSpace(updated.Name) &&
                !channel.Name.Equals(updated.Name, StringComparison.OrdinalIgnoreCase))
            {
                if (await ChannelExistsAsync(updated.Name))
                    throw new InvalidOperationException("New channel name already exists.");

                channel.Name = updated.Name;
            }

            channel.Url = updated.Url;
            channel.Description = updated.Description;

            await _context.SaveChangesAsync();
            return channel;
        }

        public async Task<bool> DeleteChannelAsync(string name)
        {
            var channel = await _context.RSSChannels
                .FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower());

            if (channel == null) return false;

            _context.RSSChannels.Remove(channel);
            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<bool> ChannelExistsAsync(string name)
        {
            return await _context.RSSChannels.AnyAsync(c =>
                c.Name.ToLower() == name.ToLower());
        }

        public async Task SubscribeUserAsync(string username, string channelName)
        {
            var user = await _context.AppUsers
                .Include(u => u.SubscribedChannels)
                .FirstOrDefaultAsync(u => u.UserName.ToLower() == username.ToLower());

            var channel = await _context.RSSChannels
                .FirstOrDefaultAsync(c => c.Name.ToLower() == channelName.ToLower());

            if (user != null && channel != null && !user.SubscribedChannels.Contains(channel))
            {
                user.SubscribedChannels.Add(channel);
                await _context.SaveChangesAsync();
            }
        }
    }
}