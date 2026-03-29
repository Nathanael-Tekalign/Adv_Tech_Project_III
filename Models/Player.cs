using Microsoft.AspNetCore.Mvc;

namespace TriviaGame.Models
{
    public class Player
    {
        public int Id { get; set; }
        public int GameSessionId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ConnectionId { get; set; } = string.Empty; // SignalR connection
        public bool IsFinished { get; set; } = false;
        public int TotalScore { get; set; } = 0;

        public GameSession GameSession { get; set; } = null!;
        public ICollection<PlayerAnswer> PlayerAnswers { get; set; } = new List<PlayerAnswer>();
    }
}
