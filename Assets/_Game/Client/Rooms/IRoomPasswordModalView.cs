using System;
using Game.Core.Rooms;

namespace Game.Client.Rooms
{
    public interface IRoomPasswordModalView
    {
        event Action CloseRequested;
        event Action<string> SubmitRequested;
        void Open(string roomTitle);
        void Close();
        void SetBusy(bool busy);
        void ShowFailure(RoomEntryFailure failure);
    }
}
