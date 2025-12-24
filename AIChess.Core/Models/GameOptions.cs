using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AIChess.Core.Models;

public class GameOptions
{
    public PlayerType White { get; set; }
    public PlayerType Black { get; set; }
    public string? WhiteModel { get; set; }
    public string? BlackModel { get; set; }

    public bool AiOnly
    {
        get
        {
            if (!string.IsNullOrEmpty(WhiteModel) && !string.IsNullOrEmpty(BlackModel))
                field = true;
            return field;
        }
        set => field = value;
    }

    public bool NoChat { get; set; }
    public bool IsValid
    {
        get
        {
            
            var hasAI = AiOnly || (White != PlayerType.Human) ^ (Black != PlayerType.Human);
            var aiHasAssociatedModel = AIHasModel();
            return hasAI && aiHasAssociatedModel;
            bool AIHasModel()
            {
                if (White == PlayerType.AIModel && string.IsNullOrEmpty(WhiteModel))
                {
                    return false;
                }
                if (Black == PlayerType.AIModel && string.IsNullOrEmpty(BlackModel))
                {
                    return false;
                }
                return true;
            }
        }
    }
}
public enum PlayerType
{
    Human,
    AIModel
}
public class ProviderAttribute : Attribute
{
    public string Name { get; }
    public ProviderAttribute(string name)
    {
        Name = name;
    }
}