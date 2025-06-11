using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;

public class GamePlaySystem : Singleton<GamePlaySystem>
{
    #region <====================| Properties |====================>
    [SerializeField] private HandController handController;

    private const            string     AndroidRatingURI = "http://play.google.com/store/apps/details?id={0}";
    private                  string     url;
    [SerializeField] private int        cubeCountClaimed = 15;
    [SerializeField] private GameObject endGamePanel;
    [SerializeField] private int        LoseOffer = 1;

    public CameraController   CameraController;
    public BoxChainReaction3D BoxChainReaction3D;

    public List<CubeTargetControl>  CurrentCubeTargets  = new ();
    public List<QueueTargetControl> CurrentQueueTargets = new ();

    public GameObject YarnWoolPrefab;
    public GameObject RollWoolPrefab;


    private GamePlayMeshController _meshController;

    private Color[] _colorTargets = new Color[4]
                                    {
                                        Color.black,
                                        Color.black,
                                        Color.black,
                                        Color.black
                                    };

    private List<Color> _colorTargetList = new List<Color>();

    private List<RollWoolAnimation> _broomBoosterPool = new List<RollWoolAnimation>();

    private                  int _currentColorClickedCount;
    private                  int _currentColorCollected;
    [SerializeField] private int _cubeTargetCountDefault = 4;


    private int  _queueCount;

    [SerializeField] private GameObject _levelPrefab;

    private bool _isPLayAnimUsingRainBow;

    private readonly int     maxCubeTarget = 4;
    private          Vector3 _scaleDefaultRollWool;


    [HideInInspector] public int TotalCubeActive;
    [HideInInspector] public int CubeReadyCount;

    public int TotalColor => _meshController.TotalColor;
    public int QueueCount => _queueCount;


    public Action<bool> LockRainBowBooster;

    //public         SoundSO   soundDict;
    public                   AudioClip woolXoayClip;
    public                   AudioClip wool1Clip;
    public AudioClip loseSound;
    private                  int       _currentOpenCubeTargetCost;
    private static           int       _replayCount = 0;

    #endregion <=============================================>


    #region UNITY_METHODS

    public override void Awake()
    {
        base.Awake();
#if UNITY_ANDROID || UNITY_EDITOR
        url = AndroidRatingURI.Replace("{0}", Application.identifier);
#endif
        IsGoToStore                 = false;
    }

    private void Start()
    {
        LoadLevel();
        //soundDict.OnInitData();
    }

    public void Reset()
    {
        _replayCount = 0;
        CameraController.ResetCamearState();
    }

    #endregion

    #region MAIN_METHODS

    public void ActiveHandController(bool isActive)
    {
        handController.SetActiveAnim(isActive);
    }
    public int  MeshCountClick         { get; private set; } = 0;

    public void GoToStore()
    {
        Luna.Unity.Playable.InstallFullGame();
        IsGoToStore = true;
    }

    public bool IsGoToStore;
    public bool OnClickMesh(Transform startPoint, List<Vector3> spiralPath, Color colorClick)
    {
        //tHÁO LEN
        MeshCountClick++;
        if (woolXoayClip != null) SoundManager.Instance.PlayOneShot(woolXoayClip, 1);
        for (int i = 0; i < CurrentCubeTargets.Count; i++)
        {
            if (!CurrentCubeTargets[i].IsActive) continue;
            if (CurrentCubeTargets[i]
               .CheckColor(colorClick))
            {
                if (!CurrentCubeTargets[i].IsReady) continue;
                CurrentCubeTargets[i]
                   .AddChild(i, out var headtrans);

                if (headtrans == null) break;
                // var rollWool = ObjectPool.Instance.PopFromPool(RollWoolPrefab, instantiateIfNone: true);
                var rollWool = Instantiate(RollWoolPrefab);
                rollWool
                   .GetComponent<RollWoolAnimation>()
                   .ResetMesh()
                   .SetParent(headtrans)
                   .SetColor(colorClick)
                   .PlayAnim(RollWoolAnimationExtensions.ParentType.CubeTarget);

                ChoseYarnWool(rollWool.transform, startPoint, spiralPath, colorClick);
                _currentColorClickedCount++;
                return true;
            }
        }

        for (int i = 0; i < CurrentQueueTargets.Count; i++)
        {
            if (CurrentQueueTargets[i]
               .AddChild(colorClick))
            {
                // var rollWool = ObjectPool.Instance.PopFromPool(RollWoolPrefab, instantiateIfNone: true);
                var rollWool = Instantiate(RollWoolPrefab);
                rollWool
                   .GetComponent<RollWoolAnimation>()
                   .ResetMesh()
                   .SetParent(CurrentQueueTargets[i].transform)
                   .SetColor(colorClick)
                   .PlayAnim(RollWoolAnimationExtensions.ParentType.CubeQueue);

                ChoseYarnWool(rollWool.transform, startPoint, spiralPath, colorClick);
                _currentColorClickedCount++;
                _queueCount++;

                if (_queueCount == CurrentQueueTargets.Count - 1)
                {
                    //PreLost
                }

                if (_queueCount >= CurrentQueueTargets.Count && CubeReadyCount == TotalCubeActive)
                {
                    //Lose
                    StartCoroutine(OnEndGameAction(false));
                    Luna.Unity.LifeCycle.GameEnded();
                }
                return true;
            }
        }
        return false;
    }

    public void TrackingEndGame(bool isQuit, bool isWin)
    {
        if (isWin) PlayerPrefs.SetInt(REPLAY_COUNT, 0);
        var state = isWin
            ? "win"
            : "lose";
        var totalColor = Math.Max(1, TotalColor);
        var timePlay   = (int)(DateTime.Now - _timeStart).TotalSeconds;
    }

    private bool _isUseBroomBooster;

    private Color _nextColor;
    private bool  _hasCube;

    public bool HasCube => _hasCube;

    public void GenNewCube(int indexCube)
    {
        if (indexCube == -1) return;


        _meshController.ColorPriorityCalculator();

        if (_meshController.CubeCount.Count > 1)
        {
            _colorTargetList = RemoveColorInList(_meshController._colorPriority, _colorTargets);
            _nextColor       = GetColorByPriority(0);
        }
        else
        {
            _nextColor = _meshController.CubeCount.FirstOrDefault()
               .Key;
        }

        _hasCube = _meshController.CubeCount.TryGetValue(_nextColor, out var cubeCount);

        if (_hasCube)
        {
            _meshController.CubeCount[_nextColor]--;
            if (_meshController.CubeCount[_nextColor] == 0)
            {
                _meshController.CubeCount.Remove(_nextColor);
            }
        }

        CurrentCubeTargets[indexCube]
           .SetColor(_nextColor);

        _colorTargets[indexCube] = _nextColor;
        CheckLockRainBowBooster();
        if (_meshController.CubeCount.Count == 0)
            LockOpenCube();
    }


    public void CheckTurnOffCube(int indexCube, bool active)
    {
        if (!active)
        {
            CurrentCubeTargets[indexCube]
               .gameObject
               .SetActive(false);
            TotalCubeActive--;
            CubeReadyCount--;
            SmoothRepositioner();
        }
    }

    public void FinishedCollectingCube()
    {
        _currentColorCollected += 3;
        if (_currentColorCollected / 3 == cubeCountClaimed)
        {
            Luna.Unity.LifeCycle.GameEnded();
            GoToStore();
        }
    }

    public void UseQueueTarget(Color nextColor, int indexCube)
    {
        // Sử dụng target trên queue
        try
        {
            foreach (QueueTargetControl t in CurrentQueueTargets)
            {
                if (t.CheckCurrentColor(nextColor))
                {
                    if (!t.IsAtive()) continue;

                    // Hiệu ứng cọc bay từ queue lên box
                    var rollWoolChild = t.transform.GetChild(1);
                    if (rollWoolChild != null)
                    {
                        var anim = rollWoolChild.GetComponent<RollWoolAnimation>();
                        anim.SetColor(nextColor);

                        Vector3 start = t.transform.position;
                        CurrentCubeTargets[indexCube]
                           .AddChild(indexCube, out var headTrans);
                        if (headTrans == null) return;

                        rollWoolChild.SetParent(headTrans);
                        Vector3 mid = (start + headTrans.position) * 0.5f + Vector3.up * 0.5f;
                        rollWoolChild.DOKill();
                        rollWoolChild
                           .DOPath(new[]
                                   {
                                       start,
                                       mid,
                                       headTrans.position
                                   }, 0.6f, PathType.CatmullRom
                                )
                           .SetEase(Ease.InOutCubic)
                           .OnComplete(() =>
                                    {
                                        anim.SnapToHole();
                                        if (wool1Clip != null)
                                            SoundManager.Instance.PlayOneShot(wool1Clip,
                                                    1
                                                );
                                    }
                                );
                    }
                    // Logic cũ
                    t.ResetDefault();
                    _queueCount--;
                }
            }
        } catch (Exception e)
        {
            Debug.LogError($"Error in UseQueueTarget: {e}");
        }
    }

    private bool _isWinGame;

    public void CheckEndGame()
    {
        // _checkQueueTarget = false;
        // foreach (QueueTargetControl t in CurrentQueueTargets)
        // {
        //     if (t.IsAtive())
        //     {
        //         _checkQueueTarget = true;
        //         break;
        //     }
        // }
        if (_isPLayAnimUsingRainBow) return;
#if UNITY_EDITOR
        Debug.Log(
                $"CheckWin: CubeCount={_meshController.CubeCount.Count},"                +
                $" ClickCount={_currentColorClickedCount}/{_meshController.TotalColor}," +
                $" BroomCount={_broomBoosterPool.Count}"
            );
#endif

        if (_currentColorCollected == _meshController.TotalColor && TotalCubeActive == CubeReadyCount)
        {
            //Win
            StartCoroutine(OnEndGameAction(true));
            Luna.Unity.LifeCycle.GameEnded();
        }
        if (_queueCount >= CurrentQueueTargets.Count && TotalCubeActive == CubeReadyCount)
        {
            //Lose
            
            StartCoroutine(OnEndGameAction(false));
            Luna.Unity.LifeCycle.GameEnded();
        }
    }

    private IEnumerator OnEndGameAction(bool isWin)
    {
        if (_isUseBroomBooster) yield break;
        if (!isWin)
            yield return new WaitForSeconds(1.2f);
        if (isWin)
        {
            if (!_isWinGame)
            {
                _isWinGame = true;
                TrackingEndGame(false, true);
            }
        }
        else
        {
            Debug.Log("Lose sound play");
            if (loseSound != null) SoundManager.Instance.PlayOneShot(loseSound,1f);
            endGamePanel.SetActive(true);
            // TrackingEndGame(false, false);
        }
    }



    private void CheckLockRainBowBooster()
    {
        var onLockRainBowBooster = true;
        foreach (var cube in _meshController.CubeCount)
        {
            if (cube.Value > 1) onLockRainBowBooster = false;
        }
        LockRainBowBooster?.Invoke(onLockRainBowBooster);
    }

    private void UnLockCubeTarget(int newCubeCount)
    {
        TotalCubeActive = 0;
        CubeReadyCount  = 0;
        for (int i = 0; i < CurrentCubeTargets.Count; i++)
        {
            CurrentCubeTargets[i]
               .gameObject
               .SetActive(true);
            CurrentCubeTargets[i]
               .SetActiveCubeTarget(i, i + 1 <= newCubeCount);
            CurrentCubeTargets[i]
               .ActiveOpenCube(i + 1 > newCubeCount);
        }
        _cubeTargetCountDefault = newCubeCount;
        SmoothRepositioner();
    }

    public void LockOpenCube()
    {
        foreach (var cube in CurrentCubeTargets)
        {
            if (cube.IsActive) continue;
            cube.ActiveOpenCube(false);
        }
    }

    private void ChoseYarnWool(Transform head, Transform tail, List<Vector3> spiralPath, Color color)
    {
        // var yarnWool       = ObjectPool.Instance.PopFromPool(YarnWoolPrefab, instantiateIfNone: true);
        var yarnWool       = Instantiate(YarnWoolPrefab);
        var YarnWoolScript = yarnWool.GetComponent<YarnWoolAnimation>();
        YarnWoolScript.SetColor(color);
        YarnWoolScript.SetPoints(spiralPath);
        YarnWoolScript.SetParent(head, tail);
    }

    #endregion

    #region HELPER_METHODS

    private List<Color> RemoveColorInList(Dictionary<Color, float> listRemove, Color[] listReference, bool alowSameColor = false)
    {
        List<Color> result       = new List<Color>();
        var         copyListMove = listRemove;
        if (!alowSameColor)
        {
            for (int i = 0; i < 4; i++)
            {
                if (listReference[i] == Color.black) continue;
                copyListMove.Remove(listReference[i]);
            }
        }
        if (copyListMove.Count == 0 && _meshController.CubeCount.Count >= 1 && _meshController.CubeCount.First()
           .Value >= 1)
        {
            foreach (var color in listRemove)
            {
                if (color.Value == 0) continue;
                result.Add(color.Key);
            }
        }
        else
        {
            foreach (var color in listRemove)
            {
                result.Add(color.Key);
            }
        }


        return result;
    }

    private Color GetColorByPriority(float priority)
    {
        if (_colorTargetList.Count == 1)
            return _meshController?._colorPriority?.FirstOrDefault()
               .Key ?? Color.black;
        Color result = Color.black;

        try
        {
            foreach (var colorPriority in _meshController._colorPriority)
            {
                result = colorPriority.Key;
                if (priority <= colorPriority.Value) break;
            }
        } catch (Exception e)
        {
            Debug.LogError($"Error in GetColorByPriority: {e}");
        }

        return result;
    }



    private void LoadLevel()
    {
        Luna.Unity.LifeCycle.GameUpdated();
        _meshController = _levelPrefab.GetComponent<GamePlayMeshController>();
        CameraController.ResetCamearState();
        UnLockCubeTarget(_cubeTargetCountDefault);
        ResetCubeTarget();
        ResetQueueTarget();
        CameraController.SpawnPoint.rotation = Quaternion.identity;
        _isWinGame                           = false;
        _meshController?.LoadcolorForMesh();
        CameraController.Setup(_levelPrefab);
        CameraController.Instance?.BlockRotate(false);

        _currentColorClickedCount = 0;
        _currentColorCollected    = 0;
        _queueCount               = 0;
        MeshCountClick            = 0;
        for (int i = 0; i < _cubeTargetCountDefault; i++)
        {
            GenNewCube(i);
            CurrentCubeTargets[i]
               .ChangeColor();
        }
        CameraController.HoldClickTime = 0;
        CameraController.StartIntro();
    }

    private static string   REPLAY_COUNT = "REPLAY_COUNT";
    public static  string   Play_ID { get; private set; }
    private static DateTime _timeStart;

    private void ResetCubeTarget()
    {
        foreach (var cube in CurrentCubeTargets)
        {
            cube.SetDefault();
        }
        _isPLayAnimUsingRainBow = false;
    }

    private void ResetQueueTarget()
    {
        _queueCount = 0;
        if (CurrentQueueTargets.Count != BoxChainReaction3D.InitialBoxCount)
        {
            BoxChainReaction3D.InitializeBoxes();
            return;
        }
        foreach (var queue in CurrentQueueTargets)
        {
            queue.ResetDefault();
        }
    }


    private List<CubeTargetControl> _activeObjects = new ();

    private Vector3 offset = new (1.2f, 0, 0);

    private void SmoothRepositioner()
    {
        CacheActiveObjects();
        Reposition();
    }

    private void CacheActiveObjects()
    {
        _activeObjects.Clear();
        foreach (var obj in CurrentCubeTargets)
        {
            if (obj != null && obj.gameObject.activeSelf)
                _activeObjects.Add(obj);
        }
    }

    [SerializeField] float spacingCubeTarget = 1f;

    private Vector3 center = new Vector3(0, 0.45f, 0);

    private void Reposition()
    {
        int activeCount = _activeObjects.Count;
        if (activeCount == 0) return;

        // Tính tổng chiều rộng cần để căn giữa
        float totalWidth = (activeCount - 1) * spacingCubeTarget;
        float startX     = -totalWidth / 2f;

        for (int i = 0; i < activeCount; i++)
        {
            var     obj        = _activeObjects[i];
            Vector3 currentPos = obj.transform.position;
            Vector3 targetPos  = new Vector3(startX + i * spacingCubeTarget, currentPos.y, currentPos.z);
            obj
               .transform
               .DOMoveX(targetPos.x, 0.3f)
               .SetEase(Ease.OutQuad)
               .OnComplete(() =>
                        {
                            obj.BakeAnimPosition();
                        }
                    );
        }
    }

    #endregion
}
