using UnityEngine;
using System.Threading.Tasks;
using Archetype.Backend;
using Archetype.Backend.API;
using Archetype.Visualization;

public class TestVisualization : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    async void Start()
    {
        // Wait for backend connection
        while (!BackendInterface.Instance.IsConnected)
            await Task.Yield();

        // Create test model
        var request = new ModelCreateRequest { /* ... */ };
        var model = await ModelAPI.CreateModel(request);
        
        // Visualize it
        await NeuralNetworkVisualizer.Instance.VisualizeNetwork(model.id);
    }
}