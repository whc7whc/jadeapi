namespace Team.Backend.Settings
{
    public class EmailSettings
    {
        public string Host { get; set; } = "";
        public int Port { get; set; } = 587;
        public string User { get; set; } = "";
        public string Pass { get; set; } = "";
        public string FromName { get; set; } = "Jade服飾";
        public string FromEmail { get; set; } = "";
    }

}
