namespace TriviaGame.Models
{
    public class GameRoom
    {
        public int Id { get; set; }
        public string RoomCode { get; set; } = "";
        public int QuizId { get; set; }
        public bool IsStarted { get; set; }
        public bool IsFinished { get; set; }
        public int CurrentQuestionIndex { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Quiz? Quiz { get; set; }
        public List<Player> Players { get; set; } = new();
    }
}