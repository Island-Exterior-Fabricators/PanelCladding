// Saves the document on the next idle tick (outside the script host's undo record) and records
// its read-only / lock state around the save. Read returns the log so far.
public static class SaveProbe
{
    private const string Key = "SaveProbe.log";

    public static object Queue(RhinoDoc doc, string tag)
    {
        uint serial = doc.RuntimeSerialNumber;
        Append(tag + " queued: " + State(doc));
        EventHandler handler = null;
        handler = (s, e) =>
        {
            RhinoApp.Idle -= handler;
            RhinoDoc d = RhinoDoc.FromRuntimeSerialNumber(serial);
            Append(tag + " before save: " + State(d));
            d.Modified = true;
            bool ok = d.Save();
            Append(tag + " save returned " + ok + ": " + State(d));
        };
        RhinoApp.Idle += handler;
        return Read();
    }

    public static object Read() => AppDomain.CurrentDomain.GetData(Key) as string ?? string.Empty;

    public static object Note(RhinoDoc doc, string tag)
    {
        Append(tag + ": " + State(doc));
        return Read();
    }

    private static void Append(string line) =>
        AppDomain.CurrentDomain.SetData(Key, (AppDomain.CurrentDomain.GetData(Key) as string ?? string.Empty) + line + "\n");

    private static string State(RhinoDoc doc) =>
        System.DateTime.Now.ToString("HH:mm:ss.fff") + " readOnly=" + doc.IsReadOnly + " locked=" + doc.IsLocked +
        " modified=" + doc.Modified + " mtime=" + System.IO.File.GetLastWriteTime(doc.Path).ToString("HH:mm:ss.fff");
}
