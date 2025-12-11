namespace tkacheva_lr2.Models
{
    public class AppUser
    {
        public int Id { get; set; }
        public string UserName { get; set; } = "";
        public string Password { get; set; } = "";
        public List<RSSChannel> SubscribedChannels { get; set; } = new();

        public bool IsPasswordStrong()
        {
            return Password.Length >= 6;
        }

        public void ChangePassword(string newPassword)
        {
            Password = newPassword;
        }

        public bool IsAdmin()
        {
            return Id == 1;
        }
    }
}
