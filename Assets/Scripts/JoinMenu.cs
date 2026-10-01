using UnityEngine;
using UnityEngine.UI;

public class JoinMenu : MonoBehaviour
{
    const int JoinCodeLength = 6;

    [SerializeField] InputField codeInput;
    [SerializeField] LandingMenu landingMenu;

    public void AddCharacter(string character)
    {
        if (codeInput == null || codeInput.text.Length >= JoinCodeLength)
            return;

        codeInput.text += character.ToUpperInvariant();
    }

    public void RemoveCharacter()
    {
        if (codeInput != null && codeInput.text.Length > 0)
            codeInput.text = codeInput.text[..^1];
    }

    public void SubmitJoinCode()
    {
        var code = codeInput != null ? codeInput.text.Trim().ToUpperInvariant() : string.Empty;
        if (code.Length != JoinCodeLength || !IsAlphanumeric(code))
        {
            Debug.LogWarning("Enter a valid 6-character alphanumeric join code.", this);
            return;
        }

        landingMenu.JoinGameWithCode(code);
    }

    bool IsAlphanumeric(string value)
    {
        foreach (var character in value)
        {
            if (!char.IsLetterOrDigit(character))
                return false;
        }

        return true;
    }
}