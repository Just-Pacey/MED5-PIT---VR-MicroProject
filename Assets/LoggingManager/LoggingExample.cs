using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LoggingExample : MonoBehaviour
{

    private LoggingManager loggingManager;

    private float gameStart;
    private float gameEnd;
    private string player = "player1";

    // Start is called before the first frame update
    void Start()
    {
        gameStart = Time.time;
        // Find the logging Manager in the scene.
        loggingManager = GameObject.Find("Logging").GetComponent<LoggingManager>();

        // Start by telling logging manager to create a new collection of logs
        // and optionally pass the column headers.
        // Column headers can also be added dynamically, but declaring headers
        // from the beginning gives best performance.
        // CreateLog (string [LogCollectionLabel], list<string> [ColumnHeaders])
        loggingManager.CreateLog("Meta", headers: new List<string>() { "GameStart", "GameEnd","PlayerName"});

        // You can also send a dictionary with multiple data entries at once.
        Dictionary<string, object> otherData = new Dictionary<string, object>() {
            {"GameStart", gameStart},
            {"PlayerName", player}
        };

        loggingManager.Log("Meta", otherData);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnApplicationQuit() {
        gameEnd = Time.time;
        loggingManager.Log("Meta", "GameEnd", gameEnd);
        // Tell the logging manager to save the data (to disk and SQL by default).
        // Saving the data is an asynchronous process. 
        // If you wish to clear the logs after saving, specify clear:true.
        loggingManager.SaveAllLogs(clear:true);

        // If you want to start a new file, you can ask loggingManager to generate
        // a new file timestamp. Saving data hereafter will go to the new file.
        loggingManager.NewFilestamp();
    }
}
