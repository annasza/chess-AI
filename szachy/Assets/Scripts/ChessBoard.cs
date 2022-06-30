using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SpecialMove
{
    None = 0,
    EnPassant,
    Castling,
    Promotion
}

public class ChessBoard : MonoBehaviour
{
    [Header("Art")]
    [SerializeField] private Material tileMaterial;
    [SerializeField] private float tileSize = 1.0f;
    [SerializeField] private float yOffset = 0.2f;
    [SerializeField] private Vector3 boardCenter = Vector3.zero;
    [SerializeField] private float deathSize = 0.5f;
    [SerializeField] private float deathSpacing = 0.4f;
    [SerializeField] private float dragOffset = 1.5f;
    [SerializeField] private GameObject victoryScreen;

    [Header("Prefabs & Materials")]
    [SerializeField] private GameObject[] prefabs;
    [SerializeField] private Material[] teamMaterials;

    // Logic
    private ChessPiece[,] chessPieces;
    private ChessPiece currentlyDragging;
    private List<Vector2Int> availableMoves = new List<Vector2Int>();
    private List<ChessPiece> deadWhites = new List<ChessPiece>();
    private List<ChessPiece> deadBlacks = new List<ChessPiece>();
    private List<Vector2Int[]> moveList = new List<Vector2Int[]>();
    private const int TILE_X = 8;
    private const int TILE_Y = 8;
    private GameObject[,] tiles;
    private Camera currentCamera;
    private Vector2Int currentHover;
    private Vector3 bounds;
    private SpecialMove specialMove;
    private bool isWhiteTurn;

    //ai
    (ChessPiece, Vector2Int) nextMove;
    [SerializeField] private int AIDepth = 5;
    private bool AITurn = false;

    // Reverse
    private int reverse = 0;
    private List<ChessPiece> chessPiecesList = new List<ChessPiece>();
    private List<bool> isKiller = new List<bool>();
    private bool killed = false;
    private List<SpecialMove> listofspecials = new List<SpecialMove>();
    [SerializeField] private bool reversed = false;

    private void Awake()
    {
        isWhiteTurn = true;

        GenerateAllTiles(tileSize, TILE_X, TILE_Y);
        SpawnAllPieces();
        PositionAllPieces();
    }

    private void Update()
    {
        if(reversed)
        {
            Reverse();
            reversed = false;
        }

        if (!currentCamera)
        {
            currentCamera = Camera.main;
            return;
        }

        RaycastHit info;
        Ray ray = currentCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out info, 100, LayerMask.GetMask("Tile", "Hover", "Highlight")))
        {
            // Get the indexes of the tile hitted
            Vector2Int hitPosition = LookupTileIndex(info.transform.gameObject);

            // If we're hovering a tile after not hovering any tiles
            if (currentHover == -Vector2Int.one)
            {
                currentHover = hitPosition;
                tiles[hitPosition.x, hitPosition.y].layer = LayerMask.NameToLayer("Hover");
            }

            // If we were already hovering a tile, change the previous one
            if (currentHover != hitPosition)
            {
                tiles[currentHover.x, currentHover.y].layer = (ContainsValidMove(ref availableMoves, currentHover)) ? LayerMask.NameToLayer("Highlight") : LayerMask.NameToLayer("Tile");
                currentHover = hitPosition;
                tiles[hitPosition.x, hitPosition.y].layer = LayerMask.NameToLayer("Hover");
            }

            // If we press down in the mouse
            if (Input.GetMouseButtonDown(0))
            {
                if (chessPieces[hitPosition.x, hitPosition.y] != null)
                {
                    // Is it our turn?
                    if ((chessPieces[hitPosition.x, hitPosition.y].team == 0 && isWhiteTurn) || (chessPieces[hitPosition.x, hitPosition.y].team == 1 && !isWhiteTurn))
                    {
                        currentlyDragging = chessPieces[hitPosition.x, hitPosition.y];

                        // Get a list of where I can go, highlight tiles as well
                        availableMoves = currentlyDragging.GetAvailableMoves(ref chessPieces, TILE_X, TILE_Y);
                        //Get a list of special moves
                        specialMove = currentlyDragging.GetSpecialMoves(ref chessPieces, ref moveList, ref availableMoves);
                        PreventCheck();
                        HighlightTiles();
                    }
                }
            }

            // If we are releasing the mouse button
            if (currentlyDragging != null && Input.GetMouseButtonUp(0))
            {
                Vector2Int previousPosition = new Vector2Int(currentlyDragging.currentX, currentlyDragging.currentY);

                bool validMove = MoveTo(currentlyDragging, hitPosition.x, hitPosition.y);
                if (!validMove)
                {
                    currentlyDragging.SetPosition(GetTileCenter(previousPosition.x, previousPosition.y));
                }
                currentlyDragging = null;

                //AI move
                AITurn = true;
                nextMove = SearchMove(AIDepth);
                MoveTo(nextMove.Item1, nextMove.Item2.x, nextMove.Item2.y);
                AITurn = false;
                //*/

                RemoveHighlightTiles();
            }
        }
        else
        {
            if (currentHover != -Vector2Int.one)
            {
                tiles[currentHover.x, currentHover.y].layer = (ContainsValidMove(ref availableMoves, currentHover)) ? LayerMask.NameToLayer("Highlight") : LayerMask.NameToLayer("Tile");
                currentHover = -Vector2Int.one;
            }

            if (currentlyDragging && Input.GetMouseButton(0))
            {
                currentlyDragging.SetPosition(GetTileCenter(currentlyDragging.currentX, currentlyDragging.currentY));
                currentlyDragging = null;
                RemoveHighlightTiles();
            }
        }

        // If we're dragging a piece
        if (currentlyDragging)
        {
            Plane horizontalPlane = new Plane(Vector3.up, Vector3.up * yOffset);
            float distance = 0.0f;
            if (horizontalPlane.Raycast(ray, out distance))
                currentlyDragging.SetPosition(ray.GetPoint(distance) + Vector3.up * dragOffset);
        }
    }


    // Generate the board
    private void GenerateAllTiles(float tileSize, int tileX, int tileY)
    {
        yOffset += transform.position.y;
        bounds = new Vector3((tileX / 2) * tileSize, 0, (tileX / 2) * tileSize) + boardCenter;

        tiles = new GameObject[tileX, tileY];

        for (int x = 0; x < tileX; x++)
            for (int y = 0; y < tileY; y++)
                tiles[x, y] = GenerateSingleTiles(tileSize, x, y);
    }

    private GameObject GenerateSingleTiles(float tileSize, int x, int y)
    {
        GameObject tileObject = new GameObject(string.Format("X:{0}. Y:{1}", x, y));
        tileObject.transform.parent = transform;

        Mesh mesh = new Mesh();
        tileObject.AddComponent<MeshFilter>().mesh = mesh;
        tileObject.AddComponent<MeshRenderer>().material = tileMaterial;

        Vector3[] vertices = new Vector3[4];
        vertices[0] = new Vector3(x * tileSize, yOffset, y * tileSize) - bounds;
        vertices[1] = new Vector3(x * tileSize, yOffset, (y + 1) * tileSize) - bounds;
        vertices[2] = new Vector3((x + 1) * tileSize, yOffset, y * tileSize) - bounds;
        vertices[3] = new Vector3((x + 1) * tileSize, yOffset, (y + 1) * tileSize) - bounds;

        int[] tris = new int[] { 0, 1, 2, 1, 3, 2 };

        mesh.vertices = vertices;
        mesh.triangles = tris;
        mesh.RecalculateNormals();

        tileObject.layer = LayerMask.NameToLayer("Tile");
        tileObject.AddComponent<BoxCollider>();

        tileObject.AddComponent<BoxCollider>();

        return tileObject;
    }

    // Spawing of the pieces
    private void SpawnAllPieces()
    {
        chessPieces = new ChessPiece[TILE_X, TILE_Y];

        int whiteTeam = 0, blackTeam = 1;

        // White Team
        chessPieces[0, 0] = SpawnSinglePiece(ChessPieceType.Rook, whiteTeam);
        chessPieces[1, 0] = SpawnSinglePiece(ChessPieceType.Knight, whiteTeam);
        chessPieces[2, 0] = SpawnSinglePiece(ChessPieceType.Bishop, whiteTeam);
        chessPieces[3, 0] = SpawnSinglePiece(ChessPieceType.Queen, whiteTeam);
        chessPieces[4, 0] = SpawnSinglePiece(ChessPieceType.King, whiteTeam);
        chessPieces[5, 0] = SpawnSinglePiece(ChessPieceType.Bishop, whiteTeam);
        chessPieces[6, 0] = SpawnSinglePiece(ChessPieceType.Knight, whiteTeam);
        chessPieces[7, 0] = SpawnSinglePiece(ChessPieceType.Rook, whiteTeam);
        for (int i = 0; i < TILE_X; i++)
        {
            chessPieces[i, 1] = SpawnSinglePiece(ChessPieceType.Pawn, whiteTeam);
        }

        // Black Team
        chessPieces[0, 7] = SpawnSinglePiece(ChessPieceType.Rook, blackTeam);
        chessPieces[1, 7] = SpawnSinglePiece(ChessPieceType.Knight, blackTeam);
        chessPieces[2, 7] = SpawnSinglePiece(ChessPieceType.Bishop, blackTeam);
        chessPieces[3, 7] = SpawnSinglePiece(ChessPieceType.Queen, blackTeam);
        chessPieces[4, 7] = SpawnSinglePiece(ChessPieceType.King, blackTeam);
        chessPieces[5, 7] = SpawnSinglePiece(ChessPieceType.Bishop, blackTeam);
        chessPieces[6, 7] = SpawnSinglePiece(ChessPieceType.Knight, blackTeam);
        chessPieces[7, 7] = SpawnSinglePiece(ChessPieceType.Rook, blackTeam);
        for (int i = 0; i < TILE_X; i++)
        {
            chessPieces[i, 6] = SpawnSinglePiece(ChessPieceType.Pawn, blackTeam);
        }
    }

    private ChessPiece SpawnSinglePiece(ChessPieceType type, int team)
    {
        ChessPiece cp = Instantiate(prefabs[(int)type - 1], transform).GetComponent<ChessPiece>();

        cp.type = type;
        cp.team = team;
        cp.GetComponent<MeshRenderer>().material = teamMaterials[team];


        return cp;
    }

    // Positioning
    private void PositionAllPieces()
    {
        for (int x = 0; x < TILE_X; x++)
            for (int y = 0; y < TILE_Y; y++)
                if (chessPieces[x, y] != null)
                    PositionSiglePieces(x, y, true);

    }

    private void PositionSiglePieces(int x, int y, bool force = false)
    {
        chessPieces[x, y].currentX = x;
        chessPieces[x, y].currentY = y; 
        chessPieces[x, y].SetPosition(GetTileCenter(x, y), force);
    }

    private Vector3 GetTileCenter(int x, int y)
    {
        return new Vector3(x * tileSize, yOffset, y * tileSize) - bounds + new Vector3(tileSize / 2, 0, tileSize / 2);
    }

    private void HighlightTiles()
    {
        for (int i = 0; i < availableMoves.Count; i++)
        {
            tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Highlight");
        }
    }
    private void RemoveHighlightTiles()
    {
        for (int i = 0; i < availableMoves.Count; i++)
        {
            tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Tile");
        }
        availableMoves.Clear();
    }

    //Checkmate
    private void CheckMate(int team)
    {
        DisplayVictory(team);
    }
    private void DisplayVictory(int winningTeam)
    {
        victoryScreen.SetActive(true);
        victoryScreen.transform.GetChild(winningTeam).gameObject.SetActive(true);
    }
    public void OnResetButton()
    {
        // UI
        victoryScreen.transform.GetChild(0).gameObject.SetActive(false);
        victoryScreen.transform.GetChild(1).gameObject.SetActive(false);
        victoryScreen.SetActive(false);

        // Fields reset
        currentlyDragging = null;
        availableMoves.Clear();
        moveList.Clear();

        // Clean Up
        for (int x = 0; x < TILE_X; x++)
        {
            for (int y = 0; y < TILE_Y; y++)
            {
                if (chessPieces[x, y] != null)
                    Destroy(chessPieces[x, y].gameObject);

                chessPieces[x, y] = null;
            }
        }

        for (int i = 0; i < deadWhites.Count; i++)
            Destroy(deadWhites[i].gameObject);
        for (int i = 0; i < deadBlacks.Count; i++)
            Destroy(deadBlacks[i].gameObject);

        deadWhites.Clear();
        deadBlacks.Clear();

        SpawnAllPieces();
        PositionAllPieces();
        isWhiteTurn = true;

    }
    public void OnExitButton()
    {
        Application.Quit();
    }

    //Special Moves
    private void ProcessSpecialMove()
    {
        if(specialMove == SpecialMove.None)
        {
            listofspecials.Add(SpecialMove.None);
        }
        if (specialMove == SpecialMove.EnPassant)
        {
            var newMove = moveList[moveList.Count - 1];
            ChessPiece myPawn = chessPieces[newMove[1].x, newMove[1].y];
            var targetPosition = moveList[moveList.Count - 2];
            ChessPiece enemyPawn = chessPieces[targetPosition[1].x, targetPosition[1].y];

            if (myPawn.currentX == enemyPawn.currentX)
            {
                if (myPawn.currentY == enemyPawn.currentY - 1 || myPawn.currentY == enemyPawn.currentY + 1)
                {
                    if (enemyPawn.team == 0)
                    {
                        deadWhites.Add(enemyPawn);
                        enemyPawn.SetScale(Vector3.one * deathSize);
                        enemyPawn.SetPosition(
                            new Vector3(8 * tileSize, yOffset, -1 * tileSize)
                            - bounds
                            + new Vector3(tileSize / 2, 0, tileSize / 2)
                            + (Vector3.forward * deathSpacing) * deadWhites.Count);
                    }
                    else
                    {
                        deadBlacks.Add(enemyPawn);
                        enemyPawn.SetScale(Vector3.one * deathSize);
                        enemyPawn.SetPosition(
                            new Vector3(-1 * tileSize, yOffset, 8 * tileSize)
                            - bounds
                            + new Vector3(tileSize / 2, 0, tileSize / 2)
                            + (Vector3.back * deathSpacing) * deadBlacks.Count);
                    }
                    chessPieces[enemyPawn.currentX, enemyPawn.currentY] = null;
                    isKiller.RemoveAt(isKiller.Count - 1);
                    isKiller.Add(true);
                    listofspecials.Add(SpecialMove.EnPassant);

                }
            }
        }
        if (specialMove == SpecialMove.Promotion)
        {
            Vector2Int[] lastMove = moveList[moveList.Count - 1];
            ChessPiece targetPawn = chessPieces[lastMove[1].x, lastMove[1].y];

            if(targetPawn.type == ChessPieceType.Pawn)
            {
                if(targetPawn.team == 0 && lastMove[1].y == 7)
                {
                    ChessPiece newQueen = SpawnSinglePiece(ChessPieceType.Queen, 0);

                    chessPieces[lastMove[1].x, lastMove[1].y].SetPosition(
                    new Vector3(0,-10, 0)
                    - bounds);                                                          //out of sight, out of mind 

                    chessPieces[lastMove[1].x, lastMove[1].y] = newQueen;
                    PositionSiglePieces(lastMove[1].x, lastMove[1].y, true);
                }
                if (targetPawn.team == 1 && lastMove[1].y == 0)
                {
                    ChessPiece newQueen = SpawnSinglePiece(ChessPieceType.Queen, 1);

                    chessPieces[lastMove[1].x, lastMove[1].y].SetPosition(
                    new Vector3(0, -10, 0)
                    - bounds);                                                          //out of sight, out of mind

                    chessPieces[lastMove[1].x, lastMove[1].y] = newQueen;
                    PositionSiglePieces(lastMove[1].x, lastMove[1].y, true);
                }
            }
            listofspecials.Add(SpecialMove.Promotion);
        }
    }
    private void PreventCheck()
    {
        ChessPiece targetKing = null;
        for (int x = 0; x < TILE_X; x++)
        {
            for (int y = 0; y < TILE_Y; y++)
                if (chessPieces[x, y] != null)
                {
                    if (chessPieces[x, y].type == ChessPieceType.King)
                    {
                        if (chessPieces[x, y].team != currentlyDragging.team)
                        {
                            targetKing = chessPieces[x, y];
                        }
                    }
                }


        }
        // Sice we are sending ref availableMoves, we will be deleting moves that are putting us in check 
        SimulateMoveForSinglePiece(currentlyDragging, ref availableMoves, targetKing);
    }
    private void SimulateMoveForSinglePiece(ChessPiece cp, ref List<Vector2Int> moves, ChessPiece targetKing)
    {
        // Save the current values, to reset after the function call

        int actualX = cp.currentX;
        int actualY = cp.currentY;
        List<Vector2Int> movesToRemove = new List<Vector2Int>();

        // Gonig through all the moves, simulate them and chech if we are in check
        for (int i = 0; i < moves.Count; i++)
        {
            int simX = moves[i].x;
            int simY = moves[i].y;

            Vector2Int kingPositionThisSim = new Vector2Int(targetKing.currentX, targetKing.currentY);
            //Did we simulate the King's move
            if (cp.type == ChessPieceType.King)
            {
                kingPositionThisSim = new Vector2Int(simX, simY);

            }
            // Copy the [,] and not a reference
            ChessPiece[,] simulation = new ChessPiece[TILE_X, TILE_Y];
            List<ChessPiece> simAttackingPieces = new List<ChessPiece>();
            for (int x = 0; x < TILE_X; x++)
            {
                for (int y = 0; y < TILE_Y; y++)
                {
                    if (chessPieces[x, y] != null)
                    {
                        simulation[x, y] = chessPieces[x, y];
                        if (simulation[x, y].team != cp.team)
                        {
                            simAttackingPieces.Add(simulation[x, y]);
                        }
                    }
                }

            }
            // Simulate taht move
            simulation[actualX, actualY] = null;
            cp.currentX = simX;
            cp.currentY = simY;
            simulation[simX, simY] = cp;

            //Did one of the piece got taken down during our simulation
            var deadPiece = simAttackingPieces.Find(c => c.currentX == simX && c.currentY == simY);
            if (deadPiece != null)
            {
                simAttackingPieces.Remove(deadPiece);
            }

            // Get all the simulated attackind pieces moves
            List<Vector2Int> simMoves = new List<Vector2Int>();
            for (int a = 0; a < simAttackingPieces.Count; a++)
            {
                var pieceMoves = simAttackingPieces[a].GetAvailableMoves(ref simulation, TILE_X, TILE_Y);
                for (int b = 0; b < pieceMoves.Count; b++)
                {
                    simMoves.Add(pieceMoves[b]);
                }
            }
            // Is the king in trouble? If so, remove the move
            if (ContainsValidMove(ref simMoves, kingPositionThisSim))
            {
                movesToRemove.Add(moves[i]);
            }
            // Restore the actual CP data
            cp.currentX = actualX;
            cp.currentY = actualY;

        }
        //Remove from the current available move list
        for (int i = 0; i < movesToRemove.Count; i++)
        {
            moves.Remove(movesToRemove[i]);
        }

    }
    private bool CheckForCheckmate()
    {
        var lastMove = moveList[moveList.Count - 1];
        int targetTeam = (chessPieces[lastMove[1].x, lastMove[1].y].team == 0) ? 1 : 0;

        List<ChessPiece> attackingPieces = new List<ChessPiece>();
        List<ChessPiece> defendingPieces = new List<ChessPiece>();
        ChessPiece targetKing = null;
        for (int x = 0; x < TILE_X; x++)
        {
            for (int y = 0; y < TILE_Y; y++)
                if (chessPieces[x, y] != null)
                {
                    if (chessPieces[x, y].team == targetTeam)
                    {
                        defendingPieces.Add(chessPieces[x, y]);
                        if (chessPieces[x, y].type == ChessPieceType.King)
                        {
                            targetKing = chessPieces[x, y];
                        }

                    }
                    else
                    {
                        attackingPieces.Add(chessPieces[x, y]);
                    }
                }
        }
        // Is the king attacked right now?
        List<Vector2Int> currentAvailableMoves = new List<Vector2Int>();
        for (int i = 0; i < attackingPieces.Count; i++)
        {
            var pieceMoves = attackingPieces[i].GetAvailableMoves(ref chessPieces, TILE_X, TILE_Y);
            for (int b = 0; b < pieceMoves.Count; b++)
            {
                currentAvailableMoves.Add(pieceMoves[b]);
            }
        }

        // Are we in check right now?
        if (ContainsValidMove(ref currentAvailableMoves, new Vector2Int(targetKing.currentX, targetKing.currentY)))
        {
            // King is under attack, can we move something to help him?
            for (int i = 0; i < defendingPieces.Count; i++)
            {
                List<Vector2Int> defendingMoves = defendingPieces[i].GetAvailableMoves(ref chessPieces, TILE_X, TILE_Y);
                // Sice we are sending ref availableMoves, we will be deleting moves that are putting us in check
                SimulateMoveForSinglePiece(defendingPieces[i], ref defendingMoves, targetKing);

                if (defendingMoves.Count != 0)
                {
                    return false;
                }
            }
            return true; //Chechmate exit 
        }

        return false;


    }

    // Operations
    private bool ContainsValidMove(ref List<Vector2Int> moves, Vector2 pos)
    {
        for (int i = 0; i < moves.Count; i++)
            if (moves[i].x == pos.x && moves[i].y == pos.y)
                return true;
        return false;
    }
    private bool MoveTo(ChessPiece cp, int x, int y)
    {
        if (!ContainsValidMove(ref availableMoves, new Vector2Int(x, y)) && reverse == 0)
        {
            if (!AITurn)             //Nie chce mi sie wglebiac w ContainsValidMove, ale AI robi tylko legalne ruchy, wiec ma w sobie to wbudowane
            {
                return false;
            }
        }

        Vector2Int previousPosition = new Vector2Int(cp.currentX, cp.currentY);

        // Is there another piece on the target position?
        if (chessPieces[x, y] != null)
        {
            ChessPiece ocp = chessPieces[x, y];

            if (cp.team == ocp.team)
                return false;

            // If its the enemy team
            if (ocp.team == 0)
            {
                if (ocp.type == ChessPieceType.King)
                    CheckMate(1);

                deadWhites.Add(ocp);
                ocp.SetScale(Vector3.one * deathSize);
                ocp.SetPosition(
                    new Vector3(8 * tileSize, yOffset, -1 * tileSize)
                    - bounds
                    + new Vector3(tileSize / 2, 0, tileSize / 2)
                    + (Vector3.forward * deathSpacing) * deadWhites.Count);
            }
            else
            {
                if (ocp.type == ChessPieceType.King)
                    CheckMate(0);

                deadBlacks.Add(ocp);
                ocp.SetScale(Vector3.one * deathSize);
                ocp.SetPosition(
                    new Vector3(-1 * tileSize, yOffset, 8 * tileSize)
                    - bounds
                    + new Vector3(tileSize / 2, 0, tileSize / 2)
                    + (Vector3.back * deathSpacing) * deadBlacks.Count);
            }

            isKiller.Add(true);
        }
        else
        {
            if(reverse == 0)
                isKiller.Add(false);
        }

        chessPieces[x, y] = cp;

        if(!killed)
            chessPieces[previousPosition.x, previousPosition.y] = null;


        PositionSiglePieces(x, y);

        isWhiteTurn = !isWhiteTurn;

        if(reverse == 0)
        {
            moveList.Add(new Vector2Int[] { previousPosition, new Vector2Int(x, y) });

            // Reverse
            chessPiecesList.Add(cp);
        }

        if (reverse == 0)
        {
            ProcessSpecialMove();
        }

        if(reverse == 0)
        {
            if (CheckForCheckmate())
            {
                CheckMate(cp.team);
            }
        }

        reverse = 0;

        return true;
    }
    private Vector2Int LookupTileIndex(GameObject hitInfo)
    {
        for (int x = 0; x < TILE_X; x++)
            for (int y = 0; y < TILE_Y; y++)
                if (tiles[x, y] == hitInfo)
                    return new Vector2Int(x, y);

        return -Vector2Int.one; // -1, -1
    }

    // Reverse
    public void Reverse()
    {
        print(listofspecials[listofspecials.Count - 1]);
        if (moveList.Count > 0)
        {
            reverse = 1;
            if (listofspecials[listofspecials.Count - 1] == SpecialMove.Promotion)
            {

                Destroy(chessPieces[moveList[moveList.Count - 1][1].x, moveList[moveList.Count - 1][1].y].gameObject);

            }
            MoveTo(chessPiecesList[chessPiecesList.Count - 1], moveList[moveList.Count - 1][0].x, moveList[moveList.Count - 1][0].y);


            if (isKiller[isKiller.Count - 1])
            {
                reverse = 1;
                killed = true;
                if (listofspecials[listofspecials.Count - 1] != SpecialMove.EnPassant)
                {
                    if (chessPiecesList[chessPiecesList.Count - 1].team == 1)
                    {
                        MoveTo(deadWhites[deadWhites.Count - 1], moveList[moveList.Count - 1][1].x, moveList[moveList.Count - 1][1].y);
                        deadWhites[deadWhites.Count - 1].SetScale(Vector3.one * 1f);
                        deadWhites.RemoveAt(deadWhites.Count - 1);
                    }
                    else
                    {
                        MoveTo(deadBlacks[deadBlacks.Count - 1], moveList[moveList.Count - 1][1].x, moveList[moveList.Count - 1][1].y);
                        deadBlacks[deadBlacks.Count - 1].SetScale(Vector3.one * 1f);
                        deadBlacks.RemoveAt(deadBlacks.Count - 1);
                    }
                }
                else
                {
                    if (chessPiecesList[chessPiecesList.Count - 1].team == 1)
                    {
                        MoveTo(deadWhites[deadWhites.Count - 1], moveList[moveList.Count - 1][1].x, moveList[moveList.Count - 1][1].y+1);
                        deadWhites[deadWhites.Count - 1].SetScale(Vector3.one * 1f);
                        deadWhites.RemoveAt(deadWhites.Count - 1);
                    }
                    else
                    {
                        MoveTo(deadBlacks[deadBlacks.Count - 1], moveList[moveList.Count - 1][1].x, moveList[moveList.Count - 1][1].y-1);
                        deadBlacks[deadBlacks.Count - 1].SetScale(Vector3.one * 1f);
                        deadBlacks.RemoveAt(deadBlacks.Count - 1);
                    }
                }

                isWhiteTurn = !isWhiteTurn;
                killed = false;
            }

            listofspecials.RemoveAt(listofspecials.Count - 1);
            isKiller.RemoveAt(isKiller.Count - 1);
            moveList.RemoveAt(moveList.Count - 1);
            chessPiecesList.RemoveAt(chessPiecesList.Count - 1);
            specialMove = SpecialMove.None;
        }

    }
    //ai

    private (ChessPiece, Vector2Int) SearchMove(int Depth)

    {
        double Alpha = double.MinValue;
        double Beta = double.MaxValue;
        (ChessPiece, Vector2Int) BestMove = (null, Vector2Int.zero);

        NegaMaxAlphaBeta(Alpha, Beta, Depth, this, ref BestMove);
        return BestMove;
    }

    private double NegaMaxAlphaBeta(double Alpha, double Beta, int Depth, ChessBoard board, ref (ChessPiece, Vector2Int) BestMove)
    {
        List<Vector2Int> trash = new List<Vector2Int>();
        double score;
        if (Depth == 0)
        {
            return EvaluatePosition(board.chessPieces);
        }
        List<(ChessPiece ,Vector2Int)> moves = GenerateMoves(1, ref board.chessPieces, board.moveList);

        foreach ((ChessPiece, Vector2Int) move in moves)
        {
            board.specialMove = move.Item1.GetSpecialMoves(ref board.chessPieces, ref board.moveList, ref trash);

            MoveTo(move.Item1, move.Item2.x, move.Item2.y);
            score = -NegaMaxAlphaBeta(-Beta, -Alpha, Depth - 1, board, ref BestMove);
            Reverse();

            if (score >= Beta)
            {
                return Beta;
            }
            if (score > Alpha)
            {
                Alpha = score;
                if (Depth == 1)
                {
                    BestMove = move;
                }
            }
        }
        return Alpha;
    }


    private List<(ChessPiece, Vector2Int)> GenerateMoves(int currentTeam, ref ChessPiece[,] board, List<Vector2Int[]> movelist)
    {
        List<Vector2Int> moves = new List<Vector2Int>();
        List<(ChessPiece, Vector2Int)> fullMoves = new List<(ChessPiece cp, Vector2Int move)>();
        (ChessPiece, Vector2Int) tuple;

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                if (chessPieces[x, y] != null)
                {
                    
                    if (chessPieces[x, y].team == currentTeam)
                    {
                        foreach (Vector2Int vector in chessPieces[x, y].GetAvailableMoves(ref chessPieces, x, y))
                        {
                            moves.Add(vector);
                        }
                        chessPieces[x, y].GetSpecialMoves(ref board, ref movelist, ref moves);
                        foreach (Vector2Int vector in moves)
                        {
                            tuple = (chessPieces[x, y], vector);
                            fullMoves.Add(tuple);
                            
                        }
                    }
                }
            }
        }
        return fullMoves;
    }

    //evaluate position
    private double EvaluatePosition(ChessPiece[,] board)
    {
        double eval = 0;
        int value = 0;
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                if (board[x, y] != null)
                {
                    if (board[x, y].type == ChessPieceType.Pawn)
                    {
                        value = 1;
                    }
                    if (board[x, y].type == ChessPieceType.Knight)
                    {
                        value = 3;
                    }
                    if (board[x, y].type == ChessPieceType.Bishop)
                    {
                        value = 3;
                    }
                    if (board[x, y].type == ChessPieceType.Rook)
                    {
                        value = 5;
                    }
                    if (board[x, y].type == ChessPieceType.Queen)
                    {
                        value = 9;
                    }
                    if (board[x, y].type == ChessPieceType.King)
                    {
                        value = 999;
                    }
                    if (board[x, y].team == 0)
                    {
                        eval += value;
                    }
                    if (board[x, y].team == 1)
                    {
                        eval -= value;
                    }
                }
            }
        }

        return eval;
    }
}
