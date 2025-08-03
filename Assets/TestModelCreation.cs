using UnityEngine;
using System.Threading.Tasks;
using Archetype.Backend;
using Archetype.Backend.API;
using Archetype.Visualization;
using System.Collections.Generic;

public class TestModelCreation : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    async void Start()
    {
        var request = new Archetype.Backend.API.ModelCreateRequest
        {
            name = "Test MLP",
            model_type = "mlp",
            architecture = new Dictionary<string, object> { ["layers"] = new List<int> { 784, 128, 64, 10 } },
            hyperparameters = new Dictionary<string, object> { ["activation"] = "relu" }
        };

        var response = await Archetype.Backend.API.ModelAPI.CreateModel(request);
        Debug.Log($"Created model: {response.id}");
    }
}
