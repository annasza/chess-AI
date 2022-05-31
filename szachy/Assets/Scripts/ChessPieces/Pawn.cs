using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Pawn : ChessPiece
{
    public override List<Vector2Int> GetAvailableMoves(ref ChessPiece[,] board, int tileCountX, int tileCountY)
    {
        List<Vector2Int> r = new List<Vector2Int>();

        int direction = (team == 0) ? 1 : -1;

        // One is front
        if (board[currentX, currentY + direction] == null)
            r.Add(new Vector2Int(currentX, currentY + direction));

        // Two in front
        if (board[currentX, currentY + direction] == null)
        {
            if (team == 0 && currentY == 1 && board[currentX, currentY + (direction *2)] == null)
                r.Add(new Vector2Int(currentX, currentY + (direction *2)));
            if (team == 1 && currentY == 6 && board[currentX, currentY + (direction * 2)] == null)
                r.Add(new Vector2Int(currentX, currentY + (direction * 2)));
        }

        // Kill  move
        if (currentX != tileCountX - 1)
            if (board[currentX + 1, currentY + direction] != null && board[currentX + 1, currentY + direction].team != team)
                r.Add(new Vector2Int(currentX + 1, currentY + direction));
        if (currentX != 0)
            if (board[currentX - 1, currentY + direction] != null && board[currentX - 1, currentY + direction].team != team)
                r.Add(new Vector2Int(currentX - 1, currentY + direction));

        return r;
    }

    public override SpecialMove GetSpecialMoves(ref ChessPiece[,] board, ref List<Vector2Int[]> moveList, ref List<Vector2Int> avaibleMoves)
    {
        int direction = (team == 0) ? 1 : -1;
        if ((team == 0 && currentY == 6) || (team == 1 && currentY == 1)) 
            return SpecialMove.Promotion;
        if (moveList.Count > 0)
        {
            Vector2Int[] lastMove = moveList[moveList.Count - 1];
            if (board[lastMove[1].x, lastMove[1].y].type == ChessPieceType.Pawn) //If last move was a pawn
            {
                if (Mathf.Abs(lastMove[0].y - lastMove[1].y) == 2) //If the last move was a 2 tile
                {
                    if (lastMove[1].y == currentY)
                    {
                       if (lastMove[1].x == currentX - 1) //Landed left
                        {
                            avaibleMoves.Add(new Vector2Int(currentX - 1, currentY + direction));
                        }
                       if (lastMove[1].x == currentX + 1) //Landed right
                        {
                            avaibleMoves.Add(new Vector2Int(currentX + 1, currentY + direction));
                        }
                        return SpecialMove.EnPassant;
                    }                  

                }
            }
        }
        return SpecialMove.None;
    }
}
