using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TriviaGame.Models;

namespace TriviaGame.Data
{
    public class TriviaGameContext : DbContext
    {
        public TriviaGameContext(DbContextOptions<TriviaGameContext> options)
            : base(options) { }

        public DbSet<Quiz> Quizzes { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<GameSession> GameSessions { get; set; }
        public DbSet<Player> Players { get; set; }
        public DbSet<PlayerAnswer> PlayerAnswers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Question>()
                .HasOne(q => q.Quiz)
                .WithMany(qz => qz.Questions)
                .HasForeignKey(q => q.QuizId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GameSession>()
                .HasOne(gs => gs.Quiz)
                .WithMany(q => q.GameSessions)
                .HasForeignKey(gs => gs.QuizId);

            modelBuilder.Entity<Player>()
                .HasOne(p => p.GameSession)
                .WithMany(gs => gs.Players)
                .HasForeignKey(p => p.GameSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PlayerAnswer>()
                .HasOne(pa => pa.Player)
                .WithMany(p => p.PlayerAnswers)
                .HasForeignKey(pa => pa.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PlayerAnswer>()
                .HasOne(pa => pa.Question)
                .WithMany(q => q.PlayerAnswers)
                .HasForeignKey(pa => pa.QuestionId);
        }
    }
}