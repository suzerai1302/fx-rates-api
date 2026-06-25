namespace FxRates.Core;

public interface ITokenIssuer
{
    string CreateToken(User user);
}
