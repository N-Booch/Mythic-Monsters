using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TitleUIEntry : MonoBehaviour
{
    public TMP_Text titleNameText;
    public Button button;

    private TitleData title;
    private TitleUIManager manager;

    public void Setup(TitleData title, TitleUIManager manager)
    {
        this.title = title;
        this.manager = manager;

        titleNameText.text = title.titleName;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClicked);
    }

    private void OnClicked()
    {
        manager.OnTitleClicked(title);
    }
}
