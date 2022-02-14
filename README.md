# WebPolyfill - A polyfills for the Unity

WebPolyfill implements web technologies on UXML and USS.

## Usage

```csharp
public class SomeUxmlEditor : EditorWindow
{
    private readonly MediaQuery _mediaQuery = new MediaQuery();

    public void CreateGUI()
    {
        // load your UXML file here...
    }

    public void OnGUI()
    {
        _mediaQuery.OnUpdate(this);
    }
}
```

## License

MIT
