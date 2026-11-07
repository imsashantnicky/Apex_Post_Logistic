using ApexPost.Courier.Presentation.Mvvm;

namespace ApexPost.Courier.Presentation.Tests.Mvvm;

public sealed class ObservableObjectTests
{
    [Fact]
    public void PropertyChanged_is_raised_only_when_the_value_changes()
    {
        var sample = new Sample();
        var changed = new List<string?>();
        sample.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        sample.Name = "CR-101";
        sample.Name = "CR-101";
        sample.Name = "CR-102";

        Assert.Equal([nameof(Sample.Name), nameof(Sample.Name)], changed);
    }

    private sealed class Sample : ObservableObject
    {
        private string _name = string.Empty;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
    }
}
