/**/
using ChessChallenge.API;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
/**/

// S. Sheta 2023 | Bot name: CaptainBotvious v3
// Given a board position, the majority of possible moves are obviously bad,
// and only a few are worth considering. This bot optimises the search by
// prioritising the most obvious moves. This is achieved by shrinking the
// search depth when looking at less obvious moves.
public class BotThirdAttempt : IChessBot
{
    public BotThirdAttempt()
    {
        rng = new System.Random();
    }

    public Move Think(Board board, Timer timer)
    {
        // setup
        isWhite = board.IsWhiteToMove;
        timeRemainingAtStart = timer.MillisecondsRemaining;
        // enter middle game
        if (gamePhase == 0 && board.PlyCount > 6) gamePhase = 1;
        // enter end game
        PieceList[] pieces = board.GetAllPieceLists();
        int numPiecesWhite = 0;
        int numPiecesBlack = 0;
        for (int i = 0; i < 6; i++) numPiecesWhite += pieces[i].Count;
        for (int i = 0; i < 6; i++) numPiecesBlack += pieces[i + 6].Count;
        if (numPiecesWhite <= 8 || numPiecesBlack <= 8) gamePhase = 2;
        // find and return move
        return FindBestMove(board, isWhite, EvaluateBoard(board, isWhite), 0, depthLimit, ref timer);
    }

    // Score the obviousness of a move based on the basic principles of chess.
    float MoveObviousness(Move move, Board board)
    {
        float obviousness = 0.0f;
        PieceType pieceType = move.MovePieceType;
        // 1. check
        board.MakeMove(move);
        if (board.IsInCheckmate())
        {
            board.UndoMove(move);
            return 100.0f;
        }
        if (board.IsDraw())
        {
            board.UndoMove(move);
            return 0.0f;
        }
        board.UndoMove(move);
        // 2. capture
        if (move.IsCapture) obviousness += GetPieceValue(move.CapturePieceType);
        // 3. attack/movement
        if (pieceType == PieceType.Pawn)
        {
            // pawns should be more active at the start and in the endgame
            obviousness += (gamePhase != 1) ? 1.5f : 1.0f;
            // extra bias for the opening
            if (move.TargetSquare.File == 3 || move.TargetSquare.File == 4) obviousness += 1.0f;
        }
        if (pieceType == PieceType.Knight || pieceType == PieceType.Bishop) obviousness += ((gamePhase == 0) ? 1.5f : 1.0f);
        if (pieceType == PieceType.Rook || pieceType == PieceType.Queen || pieceType == PieceType.King) obviousness += gamePhase / 2.0f;
        // 4. special
        if (move.IsCastles) obviousness += 3.0f;
        if (move.IsPromotion) obviousness += 10.0f;
        if (board.SquareIsAttackedByOpponent(move.TargetSquare)) obviousness -= 0.5f; // going where you can be attacked
        return Math.Max(obviousness, 0.0f);
    }

    // Evaluate the board for the specified player
    float EvaluateBoard(Board board, bool forWhite)
    {
        if (board.IsInCheckmate()) return (board.IsWhiteToMove == forWhite) ? -100.0f : 100.0f;
        if (board.IsDraw()) return 0.0f;
        PieceList[] pieces = board.GetAllPieceLists();
        float score = 0.0f;
        for (int i = 0; i < 6; i++)
        {
            // add white's score
            score += pieces[i].Count * GetPieceValue(pieces[i].TypeOfPieceInList);
            // substract black's score
            score -= pieces[i + 6].Count * GetPieceValue(pieces[i + 6].TypeOfPieceInList);
        }
        return (forWhite) ? score : -score;
    }

    // Find best move by recursion.
    Move FindBestMove(Board board, bool isWhiteToPlay, float startEval, int currentDepth, int maxDepth, ref Timer timer)
    {
        // initialize algorithm
        Move[] moves = board.GetLegalMoves();
        int numMoves = moves.Length;
        if (numMoves == 1) return moves[0];
        float currentEval = EvaluateBoard(board, isWhiteToPlay);

        // Retrieve the obviousness of all the moves and sort
        var sortedMoves = moves.Select(move => new { Move = move, Obviousness = MoveObviousness(move, board) }).OrderByDescending(item => item.Obviousness).ToArray();
        float[] moveObviousness = sortedMoves.Select(item => item.Obviousness).ToArray();
        moves = sortedMoves.Select(item => item.Move).ToArray();

        // Evaluate all the moves
        float[] moveEvals = new float[numMoves];
        for (int i = 0; i < numMoves; i++) // for all available moves
        {
            Move move = moves[i];
            board.MakeMove(move); // make the move
            if (board.IsInCheckmate())
            {
                board.UndoMove(move);
                return move;
            }
            if (board.IsDraw())
            {
                board.UndoMove(move);
                moveEvals[i] = 0.0f;
                continue;
            }
            if (currentDepth < maxDepth || isWhiteToPlay == isWhite) // evaluate with recursion
            {
                float materialBias = (EvaluateBoard(board, isWhiteToPlay) - currentEval) / 9.0f;
                float movesBias = Math.Max(30 - numMoves, 0) / 30.0f;
                float obviousnessBias = moveObviousness[i] / moveObviousness.Max();
                float timeBias = (timeRemainingAtStart - timer.MillisecondsElapsedThisTurn) / timer.GameStartTimeMilliseconds;
                float progressBias = timeBias * gamePhase / 2.0f;
                // compute the weighted depth 
                int weightedDepth = Math.Min(Math.Max(1, (int)((movesBias + materialBias + obviousnessBias) * progressBias * maxDepth / 3.0f)), depthLimit);
                // int weightedDepth = Math.Min(Math.Max(1, (int)(movesBias * obviousnessBias * progressBias * depthLimit)), depthLimit);
                Move response = FindBestMove(board, !isWhiteToPlay, -startEval, currentDepth + 1, weightedDepth, ref timer);
                board.MakeMove(response); // make the response
                // evaluate the position
                moveEvals[i] = EvaluateBoard(board, isWhiteToPlay) - startEval;
                board.UndoMove(response); // undo the response
                if (move.IsCastles && moveEvals[i] >= 0.0f) moveEvals[i] += 2.0f; // encourage castling
            }
            else // evaluate naively
            {
                moveEvals[i] = EvaluateBoard(board, isWhiteToPlay) - startEval;
            }
            board.UndoMove(move); // undo the move
        }

        // Find the most obvious among best moves and return a random one
        List<Move> bestMoves = new List<Move>();
        float bestEval = moveEvals[0];
        for (int i = 0; i < numMoves; i++)
        {
            if (moveEvals[i] > bestEval)
            {
                bestEval = moveEvals[i];
                bestMoves.Clear();
                bestMoves.Add(moves[i]);
            }
            else if (Math.Abs(moveEvals[i] - bestEval) <= 0.001f)
            {
                bestMoves.Add(moves[i]);
            }
        }
        return bestMoves[rng.Next(bestMoves.Count)];
    }

    float GetPieceValue(PieceType pieceType)
    {
        switch (pieceType)
        {
            case PieceType.Pawn:
                return 1.0f;
            case PieceType.Knight:
                return 2.75f;
            case PieceType.Bishop:
                return 3.0f;
            case PieceType.Rook:
                return 5.0f;
            case PieceType.Queen:
                return 9.0f;
            case PieceType.King:
                return 100.0f;
            default:
                return 0.0f;
        }
    }

    bool isWhite = true;
    int gamePhase = 0; // 0 for opening, 1 for midgame, 2 for endgame
    int depthLimit = 20;
    float timeRemainingAtStart;
    System.Random rng;
}