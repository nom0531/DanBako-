using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class StageSelector : MonoBehaviour
{
    /// <summary>
    /// ステージのステート。
    /// </summary>
    public enum StageState
    {
        enStop,
        enRight,
        enLeft = -1
    }

    [SerializeField,Header("ステージデータ")]
    private StageDataBase StageDataBase;
    [SerializeField,Tooltip("名称")]
    private TextMeshProUGUI StageNameText;
    [SerializeField, Tooltip("クリアタイム")]
    private TextMeshProUGUI StageClearTimeText;
    [SerializeField, Header("シフト速度")]
    private float ShiftMoveSpeed = 5.0f;
    [SerializeField, Header("SE"), Tooltip("カーソル移動音")]
    private SE SE_CursorMove;

    private const float SELECTED_SCALE = 3.0f;         // 選択されたステージの拡大率
    private const float DEFAULT_SCALE = 1.5f;          // 非選択ステージのデフォルトスケール

    private readonly Vector3 START_POSITION = new Vector3(75.0f, -20.0f, 75.0f);
    private readonly Vector3 END_POSITION = new Vector3(-75.0f, -20.0f, 75.0f);

    private GameManager m_gameManager;
    private Gamepad m_gamepad;
    private GameObject[] m_stageObjects;                // ステージオブジェクトの配列
    private Vector3[] m_movePositions;
    private StageState m_nextStage = StageState.enStop; // 次に選択するステージのステート
    private int m_currentIndex = 0;                     // 現在選択されているステージのインデックス
    private bool m_isMoving = false;                    // スライドしているかどうか
    private bool m_allMoved = true;                     //全ての動き終わったかどうか

    private void Start()
    {
        m_gameManager = GameManager.Instance;
        InitStageObjects();
        SetStagePositions();
        SpawnStages();
        UpdateStageScale();
        InitStageData();
    }

    private void Update()
    {
        SelectStageAndOption();
        MoveStage();
        UpdateStageScale();
    }

    /// <summary>
    /// ステージデータの初期化。
    /// </summary>
    private void InitStageData()
    {
        StageNameText.text = StageDataBase.stageDataList[m_currentIndex].Name;
        ClearTimeText();
    }

    /// <summary>
    /// クリアタイムの初期化。
    /// </summary>
    private void ClearTimeText()
    {
        m_gameManager.StageID = m_stageObjects[0].GetComponent<StageStatus>().MyID;     // 選択しているステージの番号を更新。

        var hour = m_gameManager.SaveDataManager.Stage[m_gameManager.StageID].ClearTime.Hour;
        var minute = m_gameManager.SaveDataManager.Stage[m_gameManager.StageID].ClearTime.Minute;
        var seconds = m_gameManager.SaveDataManager.Stage[m_gameManager.StageID].ClearTime.Seconds;

        StageClearTimeText.text = $"{hour.ToString("00")}:{minute.ToString("00")}:{seconds.ToString("00")}";
    }

    /// <summary>
    /// データベースに設定してるミニステージをステージ配列に入れる
    /// </summary>
    private void InitStageObjects()
    {
        m_stageObjects = new GameObject[StageDataBase.stageDataList.Count];
        m_movePositions = new Vector3[StageDataBase.stageDataList.Count];
        for (int i = 0; i < m_stageObjects.Length; i++)
        {
            m_stageObjects[i] = StageDataBase.stageDataList[i].Model;
        }
    }

    /// <summary>
    /// ステージのポジションを設定する
    /// </summary>
    private void SetStagePositions()
    {
        //各オブジェクト間の距離を計算
        //-2している理由は一つ座標を前にもってきていてその分間隔数が一つ減るから
        Vector3 step = (END_POSITION - START_POSITION) / (m_stageObjects.Length - 2);
        for (int i = 0; i < m_stageObjects.Length; i++)
        {
            if (i == 0)
            {
                //選択されているときに強調するためのポジション
                m_movePositions[i] = new Vector3(0.0f, -30.0f, 50.0f);
            }
            else
            {
                //それ以外のポジション
                //iに-1をしている理由は選択されている時のポジションを数えないで計算するため
                m_movePositions[i] = START_POSITION + step * (i - 1);
            }
        }
    }

    /// <summary>
    /// ステージ配列にあるミニステージを出現させる
    /// </summary>
    private void SpawnStages()
    {
        for (int i = 0; i < m_stageObjects.Length; i++)
        {
            m_stageObjects[i] = Instantiate(m_stageObjects[i], m_movePositions[i], Quaternion.identity);
            var stageStatus = m_stageObjects[i].GetComponent<StageStatus>();
            stageStatus.MyID = i;
            stageStatus.RotateFlag = true;  // オブジェクトを回転させる。
            // 座標を設定。
            m_stageObjects[i].transform.position = m_movePositions[i];
        }
    }

    /// <summary>
    /// カーソル処理。
    /// </summary>
    private void SelectStageAndOption()
    {
        if (m_isMoving == true)
        {
            return;
        }
        m_gamepad = Gamepad.current;

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            ShiftObjects(StageState.enRight);
            return;
        }
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            ShiftObjects(StageState.enLeft);
            return;
        }

        if(m_gamepad == null)
        {
            m_isMoving = true;
            return;
        }

        // 左右の矢印キー入力をチェック
        if (m_gamepad.dpad.right.wasPressedThisFrame)
        {
            // 右にシフト。
            ShiftObjects(StageState.enRight);
            return;
        }
        if (m_gamepad.dpad.left.wasPressedThisFrame)
        {
            // 左にシフト。
            ShiftObjects(StageState.enLeft);
            return;
        }
        m_isMoving = true;
    }

    /// <summary>
    /// シフト処理。
    /// </summary>
    /// <param name="stageState">次に選択するステージの方向。</param>
    private void ShiftObjects(StageState stageState)
    {
        m_isMoving = true;          // 移動を開始したらすぐにフラグを設定
        m_nextStage = stageState;   // MoveStage等の後続の関数の為に値を代入。

        // 新しい配列を作成
        GameObject[] shiftedObjects = new GameObject[m_stageObjects.Length];

        // シフト処理
        for (int i = 0; i < m_stageObjects.Length; i++)
        {
            ShiftStage(i,shiftedObjects);
        }
        // オリジナルの配列を新しい配列で置き換え
        m_stageObjects = shiftedObjects;

        InitStageData();
        SE_CursorMove.PlaySE();
    }

    /// <summary>
    /// 最後の要素を先頭に、他を1つずつずらす処理。
    /// </summary>
    private void ShiftStage(int stageNumber,GameObject[] gameObjects)
    {
        if (m_nextStage == StageState.enRight)
        {
            gameObjects[stageNumber] = m_stageObjects[(stageNumber + 1 + m_stageObjects.Length) % m_stageObjects.Length];
        }
        else if (m_nextStage == StageState.enLeft)
        {
            gameObjects[stageNumber] = m_stageObjects[(stageNumber - 1 + m_stageObjects.Length) % m_stageObjects.Length];
        }
    }

    /// <summary>
    /// ステージを動かす処理。
    /// </summary>
    private void MoveStage()
    {
        if (m_isMoving == false)
        {
            return;
        }

        for (int i = 0; i < m_stageObjects.Length; i++)
        {
            int nextStage = i;
            // ステージを動かす。
            if (m_nextStage == StageState.enRight)
            {
                nextStage = (i + (int)m_nextStage + m_stageObjects.Length) % m_stageObjects.Length;
            }
            else if (m_nextStage == StageState.enLeft)
            {
                nextStage = (i + (int)m_nextStage + m_stageObjects.Length) % m_stageObjects.Length;
            }

            m_stageObjects[i].transform.position = Vector3.Lerp(
                m_stageObjects[i].transform.position, m_movePositions[nextStage], Time.deltaTime * ShiftMoveSpeed);

            if (Vector3.Distance(m_stageObjects[i].transform.position, m_movePositions[nextStage]) >= 0.5f)
            {
                m_allMoved = false;
            }
            else
            {
                m_allMoved = true;
            }
        }

        if (m_allMoved)
        {
            UpdateIndex();
            StageNameText.text = StageDataBase.stageDataList[m_currentIndex].Name;
            m_isMoving = false;
            m_nextStage = StageState.enStop;
        }
    }

    /// <summary>
    /// ステージのスケールをいじる関数。
    /// </summary>
    private void UpdateStageScale()
    {
        // スケールをデフォルトの値で初期化。
        Vector3 targetScale = Vector3.one * DEFAULT_SCALE;

        for (int i = 0; i < m_stageObjects.Length; i++)
        {
            if (i == 0)
            {
                // 選択しているオブジェクトのスケール。
                targetScale = Vector3.one * SELECTED_SCALE;
            }
            else
            {
                targetScale = Vector3.one * DEFAULT_SCALE;
            }

            if (m_stageObjects[i].transform.localScale == targetScale)
            {
                continue;
            }
            m_stageObjects[i].transform.localScale =
                Vector3.Lerp(m_stageObjects[i].transform.localScale, targetScale, Time.deltaTime * ShiftMoveSpeed);
        }
    }

    /// <summary>
    /// インデックスを更新。
    /// </summary>
    private void UpdateIndex()
    {
        if (m_nextStage == StageState.enRight)
        {
            m_currentIndex = (m_currentIndex + 1 + m_stageObjects.Length) % m_stageObjects.Length;
        }
        else if (m_nextStage == StageState.enLeft)
        {
            m_currentIndex = (m_currentIndex - 1 + m_stageObjects.Length) % m_stageObjects.Length;
        }
    }
}
