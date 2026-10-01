using UnityEngine;

public class KeyboardKey : MonoBehaviour
{
    [SerializeField] JoinMenu joinMenu;
    [SerializeField] string keyValue;

    public void Press()
    {
        if (joinMenu != null)
            joinMenu.AddCharacter(keyValue);
    }
}
