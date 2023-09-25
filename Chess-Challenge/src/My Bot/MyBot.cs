/**/
using ChessChallenge.API;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
/**/

// S. Sheta 2023 | Bot name: CaptainBotvious
// Given a board position, the majority of possible moves are most likely
// very bad, and only a few are worth considering. So this bot optimise
// the search by prioritising the most obvious moves (or at least tries to).
public class MyBot : BotFourthAttempt { }