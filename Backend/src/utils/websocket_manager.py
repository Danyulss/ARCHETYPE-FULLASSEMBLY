from typing import List
from fastapi import WebSocket
import json
import logging


class WebSocketManager:
    """Manages WebSocket connections for real-time communication"""
    
    def __init__(self):
        self.active_connections: List[WebSocket] = []
    
    async def connect(self, websocket: WebSocket):
        """Accept new WebSocket connection"""
        await websocket.accept()
        self.active_connections.append(websocket)
        logging.info(f"WebSocket connected. Total connections: {len(self.active_connections)}")
    
    def disconnect(self, websocket: WebSocket):
        """Remove WebSocket connection"""
        if websocket in self.active_connections:
            self.active_connections.remove(websocket)
            logging.info(f"WebSocket disconnected. Total connections: {len(self.active_connections)}")
    
    async def send_personal_message(self, message: str, websocket: WebSocket):
        """Send message to specific WebSocket"""
        try:
            await websocket.send_text(message)
        except Exception as e:
            logging.error(f"Failed to send message to WebSocket: {e}")
            self.disconnect(websocket)
    
    async def broadcast(self, message: str):
        """Broadcast message to all connected WebSockets"""
        disconnected = []
        for connection in self.active_connections:
            try:
                await connection.send_text(message)
            except Exception as e:
                logging.error(f"Failed to broadcast to WebSocket: {e}")
                disconnected.append(connection)
        
        # Remove disconnected WebSockets
        for connection in disconnected:
            self.disconnect(connection)
    
    async def send_json(self, data: dict, websocket: WebSocket):
        """Send JSON data to specific WebSocket"""
        try:
            await websocket.send_text(json.dumps(data))
        except Exception as e:
            logging.error(f"Failed to send JSON to WebSocket: {e}")
            self.disconnect(websocket)
    
    async def broadcast_json(self, data: dict):
        """Broadcast JSON data to all connected WebSockets"""
        message = json.dumps(data)
        await self.broadcast(message)