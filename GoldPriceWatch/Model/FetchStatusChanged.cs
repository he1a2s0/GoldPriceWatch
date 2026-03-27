using MediatR;

namespace GoldPriceWatch.Model
{
    public enum FetchState
    {
        Waiting = 0,
        Fetching = 1
    }

    public sealed class FetchStatusChanged : INotification
    {
        public FetchState State { get; }

        public FetchStatusChanged(FetchState state)
        {
            State = state;
        }
    }
}