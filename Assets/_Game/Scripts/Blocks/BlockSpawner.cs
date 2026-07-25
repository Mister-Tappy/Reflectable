using UnityEngine;
namespace Reflectable { public sealed class BlockSpawner { public int CountForTurn(int turn)=>turn<=5?Random.Range(2,4):turn<=15?Random.Range(2,5):turn<=30?Random.Range(3,6):Random.Range(3,7); public int HpForTurn(int turn)=>5+turn*2+Random.Range(0,10); } }
