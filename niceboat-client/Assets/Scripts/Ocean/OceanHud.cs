using UnityEngine;

namespace NiceBoat
{
    public sealed class OceanHud : MonoBehaviour
    {
        public BoatController boat;
        GUIStyle title, small, number;
        string speedText = "0.0";
        float nextUpdate;
        BoatCannon cannon;
        HarborAudio harborAudio;

        void Start()
        {
            if (boat == null) return;
            cannon = boat.GetComponent<BoatCannon>();
            harborAudio = boat.GetComponent<HarborAudio>();
        }

        void Update()
        {
            if (boat != null && Time.unscaledTime >= nextUpdate)
            {
                speedText = (boat.Speed * 1.94384f).ToString("0.0");
                nextUpdate = Time.unscaledTime + 0.1f;
            }
        }

        void OnGUI()
        {
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
                small = new GUIStyle(GUI.skin.label) { fontSize = 12 };
                number = new GUIStyle(title) { fontSize = 30 };
                title.normal.textColor = new Color(0.88f, 0.84f, 0.67f);
                small.normal.textColor = new Color(0.73f, 0.76f, 0.64f);
                number.normal.textColor = title.normal.textColor;
            }
            float scale = Mathf.Clamp(Screen.height / 800f, 0.7f, 1.5f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float w = Screen.width / scale, h = Screen.height / scale;
            GUI.color = new Color(0.12f, 0.16f, 0.14f, 0.86f);
            GUI.DrawTexture(new Rect(22, 22, 300, 88), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(22, h - 97, 660, 73), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(w - 144, 22, 122, 88), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(40, 32, 270, 34), "NICEBOAT", title);
            GUI.Label(new Rect(41, 70, 270, 24), "THE OLD FISHERY  /  FREE SAIL", small);
            GUI.Label(new Rect(40, h - 86, 630, 28), "WASD / ARROWS  Sail     SPACE  Brake     Q / E  Orbit     WHEEL  Zoom     R  Reset", small);
            GUI.Label(new Rect(40, h - 61, 410, 28), harborAudio != null && harborAudio.Muted
                ? "F / LEFT CLICK / RT  Fire      M  Sound OFF"
                : "F / LEFT CLICK / RT  Fire      M  Sound ON", small);
            if (cannon != null)
            {
                GUI.Label(new Rect(440, h - 61, 140, 28), cannon.ReloadProgress >= 1f ? "CANNON READY" : "RELOADING", small);
                GUI.color = new Color(0.76f, 0.64f, 0.38f);
                GUI.DrawTexture(new Rect(577, h - 53, 82 * cannon.ReloadProgress, 5), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            GUI.Label(new Rect(w - 127, 30, 100, 42), speedText, number);
            GUI.Label(new Rect(w - 126, 75, 100, 22), "KNOTS", small);
            GUI.matrix = Matrix4x4.identity;
        }
    }
}
