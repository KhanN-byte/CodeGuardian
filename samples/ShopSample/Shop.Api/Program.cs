using Shop.Domain;

var loader = new OrderLoader();
loader.Load();

internal sealed class OrderLoader
{
    public async void Load()
    {
        try
        {
            _ = Task.FromResult(new Order()).Result;
            await Task.Delay(1);
        }
        catch
        {
        }
    }
}
