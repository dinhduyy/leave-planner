namespace LeavePlanner.Application.Abstractions;

public interface ISubdivisionCatalog
{
    bool Contains(string countryCode, string subdivisionCode);
}
