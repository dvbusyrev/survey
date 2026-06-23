using MainProject.Application.Contracts;

namespace MainProject.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTime Today => DateTime.Today;
    public DateTime Now => DateTime.Now;
}
