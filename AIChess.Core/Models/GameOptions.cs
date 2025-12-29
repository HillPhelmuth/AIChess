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

    public string? WhiteModelId
    {
        get => White == PlayerType.Human ? null : field;
        set => field = value;
    }

    public string? BlackModelId
    {
        get => Black == PlayerType.Human ? null : field;
        set => field = value;
    }
    public OpenRouterModel? WhiteModel { get; set; }
    public OpenRouterModel? BlackModel { get; set; }
    public bool AiOnly
    {
        get
        {
            if (!string.IsNullOrEmpty(WhiteModelId) && !string.IsNullOrEmpty(BlackModelId) && White == PlayerType.AIModel && Black == PlayerType.AIModel)
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
                if (White == PlayerType.AIModel && string.IsNullOrEmpty(WhiteModelId))
                {
                    return false;
                }
                if (Black == PlayerType.AIModel && string.IsNullOrEmpty(BlackModelId))
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