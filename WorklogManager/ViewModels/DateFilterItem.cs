namespace WorklogManager.ViewModels;

/// <summary>
/// Represents one date entry in the date-filter checkbox list.
/// When IsChecked changes, the parent MainViewModel rebuilds FilteredRecords.
/// </summary>
public class DateFilterItem : BaseViewModel
{
    private readonly Action _onChanged;

    public DateFilterItem(DateTime date, Action onChanged)
    {
        Date = date;
        _onChanged = onChanged;
        _isChecked = true;
    }

    public DateTime Date { get; }

    public string DateDisplay => Date.ToString("ddd  yyyy-MM-dd");

    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (SetProperty(ref _isChecked, value))
                _onChanged();
        }
    }
}
