using UnityEngine;

internal static class SolarArrayLayout
{
    internal const int Columns = 4;
    internal const int Rows = 3;
    internal const float PanelWidth = 9.57f;
    internal const float PanelDepth = 6.53f;
    internal static readonly Vector3 RearSocket = new Vector3(0f,.70f,-11.28f);
    internal static Vector3 SideSocket(int side) => new Vector3(side*20.83f,.71f,1f);
    internal static readonly Quaternion Tilt = Quaternion.Euler(21f, 0f, 0f);

    internal static Vector3 Center(int column, int row) =>
        new Vector3((column - 1.5f) * 10.48f, 4.20f, (row - 1) * 8.17f - .62f +
            (column % 2 == 0 ? -.57f : .57f));

    internal static Vector3 Sample(int column, int row, int sample)
    {
        float x = sample % 2 == 0 ? -2.58f : 2.58f;
        float z = sample < 2 ? -1.69f : 1.69f;
        return Center(column,row) + Tilt * new Vector3(x,.072f,z);
    }

    internal static bool IsPanel(Collider collider) => collider.gameObject.name == "Solar shadow panel";
}
