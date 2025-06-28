namespace Xvm.Blitz.Windows.Client.UI.ViewModels.Models;

public class PlayerViewModel
{
    public int? NumberOfBattles { get; set; }

    public string? NicknameWithClanTag { get; set; }

    public string? Tank { get; set; }

    public double? WinRate { get; set; }

    public int TableNumber { get; set; }

    public bool IsTableNumberMissing { get; set; }
}