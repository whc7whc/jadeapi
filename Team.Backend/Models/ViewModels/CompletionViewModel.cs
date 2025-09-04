﻿namespace Team.Backend.Models.ViewModels
{
    public class CompletionViewModel
    {
        public string Id { get; set; }
        public string Object { get; set; }
        public int Created { get; set; }
        public Usage Usage { get; set; }
        public List<Choices> Choices { get; set; }
    }

    public class Usage
    {
        public int prompt_tokens { get; set; }
        public int completion_tokens { get; set; }
        public int total_tokens { get; set; }
    }

    public class Choices
    {
        public Message Message { get; set; }
        public string finish_reason { get; set; }
        public int Index { get; set; }
    }

    public class Message
    {
        public string Role { get; set; }
        public string Content { get; set; }
    }
}
