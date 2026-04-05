using Microsoft.AspNetCore.SignalR;

namespace TriviaGame.Hubs
{
    public class TriviaHub : Hub
    {
        public async Task JoinRoom(string roomCode)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
        }

        public async Task LeaveRoom(string roomCode)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomCode);
        }
    }
}