using UnityEngine;
using UnityEngine.UI;

public class HostMenu : MonoBehaviour
{
    const string CodeAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    [SerializeField] Text codeText;

    public void ShowCode()
    {
        if (codeText == null)
        {
            Debug.LogWarning("HostMenu requires a code text reference.", this);
            return;
        }

        var code = new char[6];
        for (var index = 0; index < code.Length; index++)
            code[index] = CodeAlphabet[Random.Range(0, CodeAlphabet.Length)];

        codeText.text = new string(code);
    }
}
