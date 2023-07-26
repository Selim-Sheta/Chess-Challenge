/**/
using ChessChallenge.API;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
/**/

// This bot optimises the search by prioritising the most obvious moves.
// The reasoning is that given a board position, the majority of possible
// moves are obviously bad, and only a few are worth considering. So more
// time is spent on the obvious moves, and less on the rest.
public class MyBot : IChessBot
{
    public MyBot()
    {
        rng = new System.Random();
    }

    public Move Think(Board board, Timer timer)
    {
        // game setup
        if (!hasStarted)
        {
            hasStarted = true;
            maxTime = timer.MillisecondsRemaining;
            isWhite = board.IsWhiteToMove;
            Console.WriteLine("I'm White");
        }
        timeRemainingAtStart = timer.MillisecondsRemaining;
        // enter middle game
        if (gamePhase == 0 && board.PlyCount > 6) gamePhase = 1;
        // enter end game
        PieceList[] pieces = board.GetAllPieceLists();
        int numPiecesWhite = 0;
        int numPiecesBlack = 0;
        for (int i = 0; i < 6; i++)
        {
            numPiecesWhite += pieces[i].Count;
            numPiecesBlack += pieces[i + 6].Count;
        }
        if (numPiecesWhite <= 8 || numPiecesBlack <= 8) gamePhase = 2; 
        // find and return move
        return FindBestMove(board, isWhite, EvaluateBoard(board, isWhite), 0, depthLimit, timer);
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
        if (move.IsCapture) obviousness += GetPieceValue(move.CapturePieceType);
        // 3. attack
        if (pieceType == PieceType.Pawn)
        {
            // pawns should be more active at the start and in the endgame
            obviousness += (gamePhase != 1) ? 1.5f : 1.0f;
            // extra bias for the opening
            if (move.TargetSquare.File == 3 || move.TargetSquare.File == 4) obviousness += 1.0f;
        }
        if (pieceType == PieceType.Knight || pieceType == PieceType.Bishop) obviousness += ((gamePhase == 0) ? 1.5f : 1.0f);
        if (pieceType == PieceType.Rook || pieceType == PieceType.Queen) obviousness += gamePhase / 2.0f;
        if (pieceType == PieceType.King) obviousness += gamePhase / 2.0f;
        // 4. special
        if (move.IsCastles) obviousness += 3.0f;
        if (move.IsPromotion) obviousness += 10.0f;
        if (board.SquareIsAttackedByOpponent(move.TargetSquare)) obviousness -= 0.5f; // going where you can be attacked
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
    Move FindBestMove(Board board, bool isWhiteToPlay, float startEval, int currentDepth, int maxDepth, Timer timer)
    {
        // init algorithm
        Move[] moves = board.GetLegalMoves();
        int numMoves = moves.Length;
        if (numMoves == 1) return moves[0];
        float currentEval = EvaluateBoard(board, isWhiteToPlay);

        // Retrieve the obviousness of all the moves and sort
        float[] moveObviousness = moves.Select(move => MoveObviousness(move, board)).ToArray();
        var sortedArrays = moveObviousness.Select((b, index) => new { moveObviousness = b, moves = moves[index] })
                           .OrderByDescending(item => item.moveObviousness)
                           .ToArray();
        moveObviousness = sortedArrays.Select(item => item.moveObviousness).ToArray();
        moves = sortedArrays.Select(item => item.moves).ToArray();
        
        // Evaluate all the moves
        float[] moveEvals = new float[numMoves];
        float movesBias = Math.Max(30 - numMoves, 0) / 30.0f;
        for (int i = 0; i < numMoves; i++) // for all available moves
        {
            Move move = moves[i];
            if (IsCheckMate(move, board)) return move; // checkmate, return directly
            if (IsDraw(move, board))
            {
                moveEvals[i] = 0.0f; // draw, no need to evalue further
                continue;
            }
            board.MakeMove(move); // make the move
            if (currentDepth < maxDepth || isWhiteToPlay == isWhite) // evaluate with recursion
            {
                float materialBias = (EvaluateBoard(board, isWhiteToPlay) - currentEval) / 9.0f;
                float timeBias = (timeRemainingAtStart - timer.MillisecondsElapsedThisTurn) / maxTime;
                if (timeBias < 0.001f)
                {
                    Console.WriteLine("Time out!");
                }
                float obviousnessBias = moveObviousness[i] / moveObviousness.Max();
                float progressBias = timeBias * (gamePhase + 0.0f) / 2.0f;
                // compute the weighted depth 
                int weightedDepth = Math.Min(Math.Max(1, (int)((movesBias + materialBias + obviousnessBias) * progressBias * depthLimit / 3.0f)), depthLimit);
                Move response = FindBestMove(board, !isWhiteToPlay, -startEval, currentDepth + 1, weightedDepth, timer);
                board.MakeMove(response); // make the response
                // evaluate the position
                moveEvals[i] = EvaluateBoard(board, isWhiteToPlay) - startEval;
                if (move.IsCastles && moveEvals[i] >= 0.0f)
                {
                    moveEvals[i] += 2.0f; // encourage castling
                }
                board.UndoMove(response); // undo the response
            }
            else // evaluate naively
            {
                moveEvals[i] = EvaluateBoard(board, isWhiteToPlay) - startEval;
            }
            board.UndoMove(move); // undo the move
        }

        // Find the indices of the best moves
        float bestEval = moveEvals[0];
        // tolerance of 0.01
        var bestMoves = moves.Where((move, i) => Math.Abs(moveEvals[i] - bestEval) <= 0.01f).ToList();

        // TO DO: FIX THE ABOVE CODE TO
        // return the most obvious out of the best moves
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

    bool isWhite = true;
    bool hasStarted = false;
    int gamePhase = 0; // 0 for opening, 1 for midgame, 2 for endgame
    int depthLimit = 20;
    float maxTime;
    float timeRemainingAtStart;
    System.Random rng;
}