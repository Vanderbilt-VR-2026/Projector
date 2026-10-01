using System.Collections;
using UnityEngine;

public class RandomGameMenu : MonoBehaviour
{
    [SerializeField] LandingMenu landingMenu;
    [SerializeField] float placeholderSearchDuration = 3f;

    Coroutine searchRoutine;

    public void BeginSearch()
    {
        if (searchRoutine != null)
            StopCoroutine(searchRoutine);

        searchRoutine = StartCoroutine(CompletePlaceholderSearch());
    }

    IEnumerator CompletePlaceholderSearch()
    {
        yield return new WaitForSeconds(placeholderSearchDuration);
        searchRoutine = null;
        landingMenu.ShowGameFoundMenu();
    }
}