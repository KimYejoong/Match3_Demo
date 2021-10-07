using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class Match3 : MonoBehaviour
{
    #region Singleton
    private static Match3 _instance = null;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;            
        }
        else
        {
            Destroy(this.gameObject);
        }

        DontDestroyOnLoad(this.gameObject);
    }
    
    public static Match3 Instance
    {
        get
        {
            if (_instance)
                return _instance;
            
            _instance = FindObjectOfType(typeof(Match3)) as Match3;

            if (_instance == null)
            {
                Debug.Log("No Singleton object");
            }

            return _instance;
        }
    }
    #endregion

    public ArrayLayout boardLayout;

    [SerializeField]
    private ScoreManager scoreManager;

    [SerializeField]
    private TimeManager timeManager;

    [SerializeField] 
    private SFXManager sfxManager;

    [Header("UI Elements")]
    public Sprite[] pieces;
    public RectTransform gameBoard;
    public RectTransform killedBoard;    

    [Header("Prefabs")]
    public GameObject nodePiece;
    public GameObject killedPiece;

    [Header("Game Control")]
    [SerializeField]
    public float timeLimit;

    private float _timeElapsed;
    private float _timeStarted;

    private bool _isMovable;
   
    private int _combo;    

    public enum GameState
    {
        Ready = 0,
        Started,
        Closing,
        End
    }
    
    [HideInInspector]
    public enum GravityProcessingStatus
    {
        Idle,
        Locked,
        Await,
    }

    [HideInInspector]
    public GravityProcessingStatus gravityProcessingStatus; 

    [HideInInspector]
    public GameState gameState = GameState.Ready;

    public int moves = 15;
    private const int Width = 9;
    private const int Height = 12;
    private int[] _fills;

    private Node[,] _board;

    private List<NodePiece> _update;
    private List<FlippedPieces> _flipped;
    private List<NodePiece> _dead;
    private List<KilledPiece> _killed;

    private void Start()
    {
        StartGame();
    }

    private void StartGame()
    {
        _fills = new int[Width];
        _update = new List<NodePiece>();
        _flipped = new List<FlippedPieces>();
        _dead = new List<NodePiece>();
        _killed = new List<KilledPiece>();
        _isMovable = true;
        gravityProcessingStatus = GravityProcessingStatus.Idle;

        InitializeScore();
        InitializeBoard();
        VerifyBoard();
        InstantiateBoard();
    }
   
    #region Initialize
    private void InitializeScore()
    {
        _timeStarted = Time.time;
        _timeElapsed = 0;

        gameState = GameState.Started;
        _combo = 0;

        scoreManager.UpdateCombo(0, moves);
    }

    private void InitializeBoard()
    {
        _board = new Node[Width, Height];
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                _board[x, y] = new Node((boardLayout.rows[y].row[x]) ? -1 : FillPiece(), new Point(x, y));
            }
        }
    }

    private void VerifyBoard()
    {
        for (int x = 0; x < Width; x++ )
        {
            for (int y = 0; y < Height; y++)
            {
                Point p = new Point(x, y);
                int val = GetValueAtPoint(p);
                if (val <= 0) continue;

                var remove = new List<int>();

                while (IsConnected(p, true).Count > 0) // PROBLEM POINT
                {
                    val = GetValueAtPoint(p);
                    if (!remove.Contains(val))
                        remove.Add(val);
                    SetValueAtPoint(p, NewValue(ref remove));
                }                
            }
        }
    }

    private void InstantiateBoard()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                Node node = GetNodeAtPoint(new Point(x, y));
                int val = _board[x, y].value;
                if (val <= 0)
                    continue;
                GameObject p = Instantiate(nodePiece, gameBoard);
                NodePiece piece = p.GetComponent<NodePiece>();
                var rect = p.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(32 + (64 * x), -32 - (64 * y));
                piece.Initialize(val, new Point(x, y), pieces[val - 1]);
                node.SetPiece(piece);
            }
        }
    }
    #endregion

    public void ResetPiece(NodePiece piece)
    {
        piece.ResetPosition();
        _update.Add(piece);
    }

    public void FlipPieces(Point one, Point two, bool main) // main : true = actual flip, false = flip back because of no match found
    {
        if (GetValueAtPoint(one) < 0) return;

        Node nodeOne = GetNodeAtPoint(one);
        NodePiece pieceOne = nodeOne.GetPiece();

        if (GetValueAtPoint(two) > 0) // if there's actually sth to flip with
        {
            Node nodeTwo = GetNodeAtPoint(two);
            NodePiece pieceTwo = nodeTwo.GetPiece();
            nodeOne.SetPiece(pieceTwo);
            nodeTwo.SetPiece(pieceOne);

            if (main) { // only when actual flip happens 
                _flipped.Add(new FlippedPieces(pieceOne, pieceTwo));
                sfxManager.PlaySound("FlipTry");
            }

            _update.Add(pieceOne);
            _update.Add(pieceTwo);
        }
        else
        {
            ResetPiece(pieceOne);
        }
    }

    private void KillPiece(Point p, int earnedPoints)
    {
        List<KilledPiece> available = new List<KilledPiece>();
        for (int i = 0; i < _killed.Count; i++)
        {
            if (!_killed[i].falling)
                available.Add(_killed[i]);
        }
        
        KilledPiece set = null;
        if (available.Count > 0)
            set = available[0];
        else
        {
            GameObject kill = GameObject.Instantiate(killedPiece, killedBoard);
            KilledPiece kPiece = kill.GetComponent<KilledPiece>();
            set = kPiece;
            _killed.Add(kPiece);
        }
        
        int val = GetValueAtPoint(p) - 1;
        if (set != null && val >= 0 && val < pieces.Length)
            set.Initialize(GetPositionFromPoint(p), earnedPoints);        
    }

    private List<Point> IsConnected(Point p, bool main)
    {
        List<Point> connected = new List<Point>();
        int val = GetValueAtPoint(p);
        Point[] directions =
        {
            Point.Up,
            Point.Right,
            Point.Down,
            Point.Left
        };

        foreach (Point dir in directions) //Checking if there is 2 or more same shapes in the directions
        {
            List<Point> line = new List<Point>();

            int same = 0;
            for (int i = 1; i < 3; i++)
            {
                Point check = Point.Add(p, Point.Mult(dir, i));
                if (GetValueAtPoint(check) != val)
                    continue;
                line.Add(check);
                same++;
            }

            if (same > 1) //If there are more than 1 of the same shape in the direction then we know it is a match
                AddPoints(ref connected, line); //Add these points to the overarching connected list
        }

        for (int i = 0; i < 2; i++) //Checking if we are in the middle of two of the same shapes
        {
            List<Point> line = new List<Point>();

            int same = 0;
            Point[] check = { Point.Add(p, directions[i]), Point.Add(p, directions[i + 2]) };
            foreach (Point next in check) //Check both sides of the piece, if they are the same value, add them to the list
            {
                if (GetValueAtPoint(next) != val)
                    continue;
                line.Add(next);
                same++;
            }

            if (same > 1)
                AddPoints(ref connected, line);
        }

        for (int i = 0; i < 4; i++) //Check for a 2x2
        {
            List<Point> square = new List<Point>();

            int same = 0;
            int next = i + 1;
            if (next >= 4)
                next -= 4;

            Point[] check = { Point.Add(p, directions[i]), Point.Add(p, directions[next]), Point.Add(p, Point.Add(directions[i], directions[next])) };
            foreach (Point pnt in check) //Check all sides of the piece, if they are the same value, add them to the list
            {
                if (GetValueAtPoint(pnt) != val)
                    continue;
                square.Add(pnt);
                same++;
            }

            if (same > 2)
                AddPoints(ref connected, square);
        }

        if (!main)
            return connected;
        
        for (int i = 0; i < connected.Count; i++)
            AddPoints(ref connected, IsConnected(connected[i], false));

        return connected;
    }
    
    private void AddPoints(ref List<Point> points, List<Point> add)
    {
        foreach(Point p in add)
        {
            bool doAdd = true;
            for (int i = 0; i < points.Count; i++)
            {
                if (!points[i].Equals(p))
                    continue;
                doAdd = false;
                break;
            }

            if (doAdd)
                points.Add(p);
        }
    }

    private int GetValueAtPoint(Point p)
    {
        if (p.x < 0 || p.x >= Width || p.y < 0 || p.y >= Height)
            return -1;
        
        return _board[p.x, p.y].value;
    }

    private void SetValueAtPoint(Point p, int v)
    {
        _board[p.x, p.y].value = v;
    }

    private Node GetNodeAtPoint(Point p)
    {
        return _board[p.x, p.y];
    }

    private int NewValue(ref List<int> remove)
    {
        List<int> available = new List<int>();
        for (int i = 0; i < pieces.Length; i++)
        {
            available.Add(i + 1);
        }

        foreach (int i in remove)
            available.Remove(i);

        return available.Count <= 0 ? 0 : available[Random.Range(0, available.Count)];
    }

    private int FillPiece()
    {
        int val = 1;
        val = (Random.Range(0, pieces.Length)) + 1;
        return val;
    }

    private void Update()
    {
        if (gameState == GameState.Started) {
            _timeElapsed = Time.time - _timeStarted;                        
            timeManager.SetTime(timeLimit - _timeElapsed, timeLimit);

            if (_timeElapsed >= timeLimit) // Time over, game set
            {
                _timeElapsed = timeLimit;
                gameState = GameState.Closing;
            }
        }

        List<NodePiece> finishedUpdating = new List<NodePiece>();

        if (gravityProcessingStatus == GravityProcessingStatus.Await)
            gravityProcessingStatus = GravityProcessingStatus.Idle;
        
        for (int i = 0; i < _update.Count; i++)
        {
            NodePiece piece = _update[i];
            if (piece.UpdatePiece())
                continue;
            
            if (piece.isFalling)
                piece.isFalling = false;
            else
                finishedUpdating.Add(piece);
        }

        int totalCount = _update.Count + finishedUpdating.Count;

        if (gravityProcessingStatus != GravityProcessingStatus.Idle)
            return;

        for (int i = 0; i < finishedUpdating.Count; i++)
        {
            NodePiece piece = finishedUpdating[i];
            FlippedPieces flip = GetFlipped(piece);
            NodePiece flippedPiece = null;

            List<Point> connected = IsConnected(piece.index, true);
            bool wasFlipped = (flip != null);

            int x = (int)piece.index.x;
            _fills[x] = Mathf.Clamp(_fills[x] - 1, 0, Width);

            if (wasFlipped) { 
                flippedPiece = flip.GetOtherPiece(piece);
                AddPoints(ref connected, IsConnected(flippedPiece.index, true)); // to check if flipped pieces also matches
            }

            if (connected.Count == 0) // case : no match at all
            {
                if (wasFlipped) // if we "flipped" to make this update
                {
                    FlipPieces(piece.index, flippedPiece.index, false); // flip it back(main == false)
                    _combo = 0; // Reset Combo count                    
                    scoreManager.UpdateCombo(_combo, moves);
                    sfxManager.PlaySound("MatchFail");
                }
            }
            else // case : match happens
            {
                int brokenPieces = connected.Count;
                int earnedPoints = CalcScore(_combo);

                foreach (Point pnt in connected) // remove the node pieces
                {
                    KillPiece(pnt, earnedPoints);
                    Node node = GetNodeAtPoint(pnt);
                    NodePiece nodePiece = node.GetPiece();
                    if (nodePiece != null)
                    {
                        nodePiece.gameObject.SetActive(false);
                        _dead.Add(nodePiece);
                    }

                    node.SetPiece(null);                    
                }

                scoreManager.AddPoint(earnedPoints * brokenPieces); // Add Points
                _combo++;                

                scoreManager.UpdateCombo(_combo, moves);

                sfxManager.PlaySound("MatchSuccess");

                StartCoroutine(ApplyGravityToBoard());
            }
            
            _flipped.Remove(flip); // remove the flip after update
            _update.Remove(piece);            
        }
        
        _isMovable = (totalCount == 0);

        if (gameState != GameState.Closing)
            return;

        if (totalCount != 0)
            return;
        
        StartCoroutine(AddScoreFromRemainTime(timeLimit - _timeElapsed));
        sfxManager.PlaySound("GameOver");
        timeManager.GameEnd();                
        gameState = GameState.End;
    }

    public bool IsMovable()
    {
        return _isMovable;
    }

    private IEnumerator AddScoreFromRemainTime(float time)
    {
        if (time > 0)
        {
            float timeRemain = time;
            float delay = 1.0f;
            float offset = timeRemain / delay;

            float timeTemp = timeRemain;

            yield return new WaitForSeconds(0.5f);            

            while (timeTemp > 0)
            {
                timeTemp -= offset * Time.deltaTime;
                timeManager.SetTime(timeTemp, timeLimit);
                yield return null;
            }
            scoreManager.AddPoint(CalcScoreFromRemainTime(timeRemain));
        }
        else
        {
            gameState = GameState.End;
        }
    }

    private int CalcScore(int combo)
    {
        return (Mathf.Min(8, combo) + 1) * 25;
    }

    private int CalcScoreFromRemainTime(float time)
    {
        return (int)Mathf.Floor(time) * 100;
    }

    private IEnumerator ApplyGravityToBoard()
    {
        int[] chunkSep = new int[Width]; // pieces dropping from upper boundary of the board

        for (int x = 0; x < Width; x++)
        {
            chunkSep[x] = -1;
            bool escape = false;
            
            for (int y = (Height - 1); y >= 0; y--) // from bottom to top
            {
                if (escape)
                    break;
                
                Point p = new Point(x, y);
                Node node = GetNodeAtPoint(p);
                int val = GetValueAtPoint(p);

                if (val != 0) // if it is filled with sth, then just continue to next
                    continue;

                for (int ny = (y - 1); ny >= -1; ny--)
                {
                    Point next = new Point(x, ny);
                    int nextVal = GetValueAtPoint(next);
                    if (nextVal == 0) // if above cell is empty then continue (till meeting some other piece)
                        continue;

                    if (nextVal != -1) // if any piece exists above, get that piece and make it drop
                    {
                        Node got = GetNodeAtPoint(next);
                        NodePiece piece = got.GetPiece();

                        piece.isFalling = true;
                        node.SetPiece(piece);
                        _update.Add(piece);

                        got.SetPiece(null);
                    }
                    else // case: top of the board or red cell above
                    {
                        chunkSep[x] = y;
                        escape = true;
                    }
                    
                    break;
                }
            }
        }

        gravityProcessingStatus = GravityProcessingStatus.Locked; 
        // Set status to "Locked" to prevent matching while waiting for next step(drop new piece from above)
        yield return new WaitForSeconds(0.3f);
        gravityProcessingStatus = GravityProcessingStatus.Await;
        // Set status back to "Await" so that makes it possible to check whether the process is over from individual pieces

        for (int xx = 0; xx < Width; xx++)
        {
            if (chunkSep[xx] == -1)
                continue;
            
            for (int yy = chunkSep[xx] + 1; yy > -1; yy--) // from the top of "dropped pieces" to upper boundary of the board
            {
                Point p = new Point(xx, yy);
                Node node = GetNodeAtPoint(p);
                int val = GetValueAtPoint(p);

                if (val != 0) // if it is filled with sth, then just continue to next
                    continue;
                
                NodePiece piece;
                Point fallPoint = new Point(xx, (-1 - _fills[xx]));

                int newVal = FillPiece();
                if (_dead.Count > 0)
                {
                    NodePiece revived = _dead[0];
                    revived.gameObject.SetActive(true);
                    revived.rect.anchoredPosition = GetPositionFromPoint(fallPoint);
                    piece = revived;
                            
                    _dead.RemoveAt(0);
                }
                else // in case when no dead piece is available
                {
                    GameObject obj = Instantiate(nodePiece, gameBoard);
                    NodePiece n = obj.GetComponent<NodePiece>();
                    RectTransform rect = obj.GetComponent<RectTransform>();
                    rect.anchoredPosition = GetPositionFromPoint(fallPoint);
                    piece = n;
                }

                piece.Initialize(newVal, p, pieces[newVal - 1]);

                Node hole = GetNodeAtPoint(p);
                hole.SetPiece(piece);
                ResetPiece(piece);
                piece.isFalling = true;
                _fills[xx]++;
            }
        }        
    }

    private FlippedPieces GetFlipped(NodePiece p)
    {
        FlippedPieces flip = null;
        for (int i = 0; i < _flipped.Count; i++)
        {
            if (_flipped[i].GetOtherPiece(p) == null)
                continue;
            flip = _flipped[i];
            break;
        }
        return flip;
    }
    
    public Vector2 GetPositionFromPoint(Point p)
    {
        return new Vector2(32 + (64 * p.x), -32 - (64 * p.y));
    }

    private void ShowHint()
    {
        Debug.Log("Hint!");
    }

}

[System.Serializable]
public class Node
{
    public int value; // 0 == Blank, 1 == Cube, 2 == Sphere, 3 == Cylinder, 4 == Pyramid, 5 == Diamond, -1 == Hole;
    public Point index;
    private NodePiece _piece;

    public Node(int v, Point i)
    {
        value = v;
        index = i;
    }

    public void SetPiece(NodePiece p)
    {
        _piece = p;
        value = (_piece == null) ? 0 : _piece.value;
        if (_piece == null) return;
        _piece.SetIndex(index);
    }

    public NodePiece GetPiece()
    {
        return _piece;
    }
}

[System.Serializable]
public class FlippedPieces
{
    public NodePiece one;
    public NodePiece two;

    public FlippedPieces(NodePiece o, NodePiece t)
    {
        one = o;
        two = t;
    }

    public NodePiece GetOtherPiece(NodePiece p)
    {
        if (p == one)
            return two;
        else if (p == two)
            return one;
        else
            return null;
    }
}