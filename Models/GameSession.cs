using Microsoft.AspNetCore.Mvc;
using System.Numerics;

namespace TriviaGame.Models
{
    public enum GameState
    {
        Lobby,
        InProgress,
        Finished
    }

    public class GameSession
    {
        public int Id { get; set; }
        public int QuizId { get; set; }
        public string RoomCode { get; set; } = string.Empty;
        public GameState State { get; set; } = GameState.Lobby;
        public int CurrentQuestionIndex { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? QuestionOrderJson { get; set; }

        public Quiz Quiz { get; set; } = null!;
        public ICollection<Player> Players { get; set; } = new List<Player>();
    }
}
