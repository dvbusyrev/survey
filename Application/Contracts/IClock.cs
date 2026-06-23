namespace MainProject.Application.Contracts;

public interface IClock
{
    DateTime Today { get; }
    DateTime Now { get; }
}
