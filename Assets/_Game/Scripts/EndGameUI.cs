using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class EndGameUI : MonoBehaviour
{
    [SerializeField] private Button  replayButton;
    [SerializeField] private Vector3 maxScale      = new Vector3(1.2f, 1.2f, 1.2f);
    [SerializeField] private Vector3 minScale      = new Vector3(1f,   1f,   1f);
    [SerializeField] private float   scaleDuration = 0.5f;
    
    // Start is called before the first frame update
    void Awake()
    {
        replayButton.onClick.AddListener(GoToStore);
        
    }
    
    private void OnEnable()
    {
        
        StartScalingAnimation();
    }

    private void OnDestroy()
    {
        replayButton.onClick.RemoveListener(GoToStore);
    }
    
    private void GoToStore()
    {
        GamePlaySystem.Instance.GoToStore();
    }
    
    private void StartScalingAnimation()
    {
        replayButton.transform.DOScale(maxScale, scaleDuration)
           .SetEase(Ease.InOutSine)
           .OnComplete(() =>
            {
                replayButton.transform.DOScale(minScale, scaleDuration)
                   .SetEase(Ease.InOutSine)
                   .OnComplete(StartScalingAnimation);
            });
    }
}
