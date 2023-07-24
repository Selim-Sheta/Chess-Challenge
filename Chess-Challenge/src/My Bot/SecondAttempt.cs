using ChessChallenge.API;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;

// This bot optimises the search by prioritising the most obvious moves.
// The reasoning is that given a board position, the majority of possible
// moves are obviously bad, and only a few are worth considering. So more
// time is spent on the obvious moves, and less on the rest.
public class BotSecondAttempt : IChessBot
{
    public BotSecondAttempt()
    {
        rng = new System.Random();
    }

    public Move Think(Board board, Timer timer)
    {
        if (!hasStarted) {
            hasStarted = true;
            maxTime = timer.MillisecondsRemaining;
            gamePhase = 0; // enter opening
        }
        timeRemainingAtStart = timer.MillisecondsRemaining;
        if (gamePhase == 0 && board.PlyCount > 6) gamePhase = 1; // enter middle game
        if (IsEndGame(board)) gamePhase = 2; // enter end game
        // return the best of the obvious moves
        Move bestMove = FindBestMove(board, board.IsWhiteToMove, EvaluateBoard(board, board.IsWhiteToMove), 0, 2, timer);
        newPos = bestMove.TargetSquare;
        return bestMove;
    }

    // Compute the obviousness score of a move based on the basic
    // principles of chess.
    float MoveObviousness(Move move, Board board)
    {
        float obviousness = 0.0f;
        PieceType pieceType = move.MovePieceType;
        // 1. check
        if (IsCheckMate(move, board)) return 100.0f;
        if (IsCheck(move, board)) obviousness += 3.0f;
        // 2. capture
        if (move.IsCapture) obviousness += 2.0f;
        // 3. attack
        if (pieceType == PieceType.Pawn)
        {
            // pawns should be more active at the start and in the endgame
            obviousness += (gamePhase != 1) ? 1.5f : 1.0f;
            // extra bias for the opening
            if (move.TargetSquare.File == 3 || move.TargetSquare.File == 4) obviousness += 1.0f;
        }
        if (pieceType == PieceType.Knight || pieceType == PieceType.Bishop) obviousness += (gamePhase == 0) ? 1.5f : 1.0f;
        if (pieceType == PieceType.Rook || pieceType == PieceType.Queen) obviousness += ((gamePhase < 2) ? 0.5f : 1.5f);
        if (pieceType == PieceType.King) obviousness += ((gamePhase < 2) ? 0.5f : 1.25f);
        // 4. special
        if (move.IsCastles) obviousness += 2.0f;
        if (move.IsPromotion) obviousness += 10.0f;
        if (board.SquareIsAttackedByOpponent(move.TargetSquare)) obviousness -= 0.25f; // avoid going where you can be attacked
        if (move.StartSquare == newPos) obviousness -= 0.5f; // avoid moving the same piece twice
        return Math.Max(obviousness, 0.0f);
    }

    // Evaluate the board for the specified player
    float EvaluateBoard(Board board, bool isWhiteToMove)
    {
        if (board.IsInCheckmate()) return isWhiteToMove ? -100.0f : 100.0f;
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
        return isWhiteToMove ? score : -score;
    }

    // Find best move by recursion.
    Move FindBestMove(Board board, bool isWhiteToPlay, float currentEval, int currentDepth, int maxDepth, Timer timer)
    {
        // init algorithm
        float startEval = EvaluateBoard(board, isWhiteToPlay);
        Move[] moves = board.GetLegalMoves();
        int numMoves = moves.Length;
        if (numMoves == 1) return moves[0];
        float timeCoeff = (timeRemainingAtStart - timer.MillisecondsElapsedThisTurn) / maxTime;

        // Retrieve the obviousness of all the moves
        float[] moveObviousness = new float[numMoves];
        for (int i = 0; i < numMoves; i++) moveObviousness[i] = MoveObviousness(moves[i], board);

        // Evaluate all the moves
        float[] moveEvals = new float[numMoves];
        for (int i = 0; i < numMoves; i++)
        {   
            Move move = moves[i];
            if (IsCheckMate(move, board)) return move; // checkmate
            if (IsDraw(move, board)) {
                moveEvals[i] = 0.0f; // draw
                continue;
            }
            board.MakeMove(move);
            if (currentDepth < maxDepth)
            {
                // encourage material gain
                float bias = gamePhase * (EvaluateBoard(board, isWhiteToPlay) - startEval) / 9.0f;
                // encourage obvious moves
                bias += 2.0f * moveObviousness[i] / moveObviousness.Max();
                int weightedDepth = 2 + Math.Min(2 * (int)(0.5f * bias * timeCoeff), 4);
                Move response = FindBestMove(board, !isWhiteToPlay, currentEval, currentDepth + 1, weightedDepth, timer);
                board.MakeMove(response);
                // evaluate the position
                moveEvals[i] = EvaluateBoard(board, isWhiteToPlay) - currentEval;
                if (move.IsCastles && moveEvals[i] >= currentEval) moveEvals[i] += 1.0f; // encourage castling
                board.UndoMove(response);
            }
            else
            {
                // naive evaluation
                moveEvals[i] = EvaluateBoard(board, isWhiteToPlay) - currentEval;
            }

            board.UndoMove(move);
        }

        // Find the indices of the best moves
        float bestEval = moveEvals.Max();
        List<int> bestMoves = new List<int>();
        for (int i = 0; i < numMoves; i++)
        {
            if (Math.Abs(moveEvals[i] - bestEval) <= 0.01f) // tolerance of 0.01
            {
                bestMoves.Add(i);
            }
        }

        // Find the indices of most obvious among best moves
        float bestObviousness = bestMoves.Max(i => moveObviousness[i]);
        List<int> mostObviousMoves = bestMoves.Where(i => Math.Abs(moveObviousness[i] - bestObviousness) <= 0.01f).ToList();

        return moves[mostObviousMoves[rng.Next(mostObviousMoves.Count)]];
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

    bool IsCheck(Move move, Board board)
    {
        board.MakeMove(move);
        bool isCheck = board.IsInCheck();
        board.UndoMove(move);
        return isCheck;
    }

    bool IsCheckMate(Move move, Board board)
    {
        board.MakeMove(move);
        bool isCheckMate = board.IsInCheckmate();
        board.UndoMove(move);
        return isCheckMate;
    }

    bool IsDraw(Move move, Board board)
    {
        board.MakeMove(move);
        bool isDraw = board.IsDraw();
        board.UndoMove(move);
        return isDraw;
    }

    bool IsEndGame(Board board)
    {
        PieceList[] pieces = board.GetAllPieceLists();
        int numPieces = 0;
        for (int i = 0; i < 12; i++) numPieces += pieces[i].Count;
        return (numPieces <= 16);
    }

    bool hasStarted = false;
    int gamePhase = 0; // 0 for opening, 1 for midgame, 2 for endgame
    float timeRemainingAtStart;
    float maxTime;
    System.Random rng;
    Square newPos;
}