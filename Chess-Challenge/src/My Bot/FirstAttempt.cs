using ChessChallenge.API;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;

namespace ChessChallenge.Example
{
    public class BotFirstAttempt : IChessBot
    {
        public BotFirstAttempt()
        {
            rng = new System.Random();
        }

        public Move Think(Board board, Timer timer)
        {
            if (gamePhase == 0 && board.PlyCount > 6) gamePhase = 1; // enter middle game
            if (IsEndGame(board)) gamePhase = 2; // enter end game
            isWhite = board.IsWhiteToMove;
            confidence = 0.0f;
            Move[] moves = board.GetLegalMoves();

            // find the most obvious moves
            float[] moveObviousness = new float[moves.Length];
            for (int i = 0; i < moves.Length; i++) moveObviousness[i] = MoveObviousness(moves[i], board);
            int[] mostObviousMoves = SlowSort(moveObviousness);

            // evaluate the most obvious moves
            float[] moveEvals = new float[mostObviousMoves.Length];
            for (int i = 0; i < mostObviousMoves.Length; i++) moveEvals[i] = EvaluateMove(moves[mostObviousMoves[i]], board);
            int[] bestMoves = SlowSort(moveEvals);

            // return the best of the obvious moves
            return moves[mostObviousMoves[bestMoves[0]]];
        }

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
            // must add a bit of randomness to avoid always playing the same moves
            return obviousness * (float)(0.5 + rng.NextDouble());
        }

        float EvaluateBoard(Board board, bool isWhiteToMove)
        {
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

        float EvaluateMove(Move move, Board board)
        {
            float evalNow = EvaluateBoard(board, isWhite);
            if (IsCheckMate(move, board)) return 100.0f; // checkmate
            if (IsDraw(move, board)) return 0.0f; // draw
            board.MakeMove(move);
            Move response = FindTheirBestMove(board);
            board.MakeMove(response);
            float evaluation = EvaluateBoard(board, isWhite) - evalNow;
            board.UndoMove(response);
            board.UndoMove(move);
            //float obviousness = MoveObviousness(move, board);

            // if this seems like a good move and the bot is confident, it will pay more attention to safe, obvious moves
            // if the bot is losing, it will prefer slightly riskier (less obvious) moves.
            return evaluation;
        }

        Move FindMyBestMove(Board board)
        {
            float startEval = EvaluateBoard(board, isWhite);
            Move[] moves = board.GetLegalMoves();

            // find the most obvious moves
            float[] moveObviousness = new float[moves.Length];
            for (int i = 0; i < moves.Length; i++) moveObviousness[i] = MoveObviousness(moves[i], board);
            int[] mostObviousMoves = SlowSort(moveObviousness);

            // evaluate the most obvious moves
            float[] moveEvals = new float[mostObviousMoves.Length];
            for (int i = 0; i < moves.Length; i++)
            {
                board.MakeMove(moves[i]);
                moveEvals[i] = EvaluateBoard(board, isWhite) - startEval;
                board.UndoMove(moves[i]);
            }
            int[] bestMoves = SlowSort(moveEvals);

            // return the best of the obvious moves
            return moves[mostObviousMoves[bestMoves[0]]];
        }

        Move FindTheirBestMove(Board board)
        {
            float startEval = EvaluateBoard(board, !isWhite);
            Move[] moves = board.GetLegalMoves();

            float[] moveEvals = new float[moves.Length];
            for (int i = 0; i < moves.Length; i++)
            {
                board.MakeMove(moves[i]);
                moveEvals[i] = EvaluateBoard(board, !isWhite) - startEval;
                board.UndoMove(moves[i]);
            }
            int[] bestMoves = SlowSort(moveEvals);
            return moves[bestMoves[0]];
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
        int[] SlowSort(float[] arr)
        {
            int N = arr.Length;
            int[] result = new int[N];
            for (int i = 0; i < N; i++)
            {
                int maxIndex = 0;
                for (int j = 0; j < N; j++)
                {
                    if (arr[j] > arr[maxIndex]) maxIndex = j;
                }
                result[i] = maxIndex;
                arr[maxIndex] = float.MinValue;
            }
            return result;
        }

        int gamePhase = 0; // 0 for opening, 1 for midgame, 2 for endgame
        bool isWhite = true;
        float confidence = 0.0f;
        System.Random rng;
    }
}