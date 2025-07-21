use std::env;
use std::sync::Arc;
use anyhow::Result;
use log::info;
use warp::Filter;

mod app_state; // Declare the app_state module
use app_state::{AppState}; // Import necessary items

#[tokio::main]
async fn main() -> Result<()> {
    // Parse command line arguments
    let args: Vec<String> = env::args().collect();
    let headless = args.contains(&"--headless".to_string());
    let port = args.iter()
        .position(|arg| arg == "--port")
        .and_then(|i| args.get(i + 1))
        .and_then(|port_str| port_str.parse::<u16>().ok())
        .unwrap_or(8080);

    if headless {
        // Run as HTTP server for Unity communication
        start_http_server(port).await?;
    } else {
        // Run normal Tauri GUI application
        let app_state = AppState::new().await?;
        
        tauri::Builder::default()
            .manage(app_state)
            .invoke_handler(tauri::generate_handler![
                // ... existing command handlers
            ]);
            //.run(tauri::generate_context!())?;
    }
    
    Ok(())
}

// Add HTTP server for Unity communication
async fn start_http_server(port: u16) -> Result<()> {
    use warp::Filter;
    
    info!("🚀 Starting Archetype backend server on port {}", port);
    
    let app_state = AppState::new().await?;
    let app_state = Arc::new(app_state);
    
    // Health check endpoint
    let health = warp::path("health")
        .and(warp::get())
        .map(|| warp::reply::with_status("OK", warp::http::StatusCode::OK));
    
    // Command endpoint
    let commands = warp::path("command")
        .and(warp::post())
        .and(warp::body::json())
        .and(with_state(app_state.clone()))
        .and_then(handle_command);
    
    let routes = health.or(commands);
    
    warp::serve(routes)
        .run(([127, 0, 0, 1], port))
        .await;
    
    Ok(())
}

fn with_state(
    state: Arc<AppState>,
) -> impl Filter<Extract = (Arc<AppState>,), Error = std::convert::Infallible> + Clone {
    warp::any().map(move || state.clone())
}

async fn handle_command(
    request: serde_json::Value,
    state: Arc<AppState>,
) -> Result<impl warp::Reply, warp::Rejection> {
    let command = request["command"].as_str().unwrap_or("");
    let parameters = &request["parameters"];
    
    let response = match command {
        "get_gpu_info" => {
            let gpu_manager = state.gpu_manager.read().await;
            let gpu_info = gpu_manager.get_gpu_info().await.unwrap_or_default();
            serde_json::json!({ "gpus": gpu_info })
        }
        "select_gpu_device" => {
            let adapter_index = parameters["adapter_index"].as_u64().unwrap_or(0) as usize;
            let mut gpu_manager = state.gpu_manager.write().await;
            let _ = gpu_manager.select_gpu_device(adapter_index).await;
            serde_json::json!({ "success": true })
        }
        "get_available_plugins" => {
            let plugin_manager = state.plugin_manager.read().await;
            let plugins = plugin_manager.get_available_plugins().await.unwrap_or_default();
            serde_json::json!({ "plugins": plugins })
        }
        "load_plugin" => {
            let plugin_name = parameters["plugin_name"].as_str().unwrap_or("");
            let mut plugin_manager = state.plugin_manager.write().await;
            let _ = plugin_manager.load_plugin(plugin_name).await;
            serde_json::json!({ "success": true })
        }
        "get_performance_metrics" => {
            let gpu_manager = state.gpu_manager.read().await;
            let metrics = gpu_manager.get_performance_metrics().await.unwrap_or_default();
            serde_json::json!(metrics)
        }
        _ => {
            serde_json::json!({ "error": format!("Unknown command: {}", command) })
        }
    };
    
    Ok(warp::reply::json(&response))
}