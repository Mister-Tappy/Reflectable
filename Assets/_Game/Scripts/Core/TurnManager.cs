using System;
namespace Reflectable { public sealed class TurnManager { public int Turn {get;private set;} public GameState State {get;private set;} public event Action<GameState> Changed; public void Begin(){Turn++;Set(GameState.Aiming);} public void Set(GameState s){State=s;Changed?.Invoke(s);} public void Reset(){Turn=0;Set(GameState.MainMenu);} } }
