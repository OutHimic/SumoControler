from flask import Flask, request, jsonify, send_from_directory
import os
import json
from datetime import datetime

app = Flask(__name__)

# 确保目录存在
os.makedirs("static", exist_ok=True)
os.makedirs("data", exist_ok=True)

@app.route('/static/<path:filename>')
def serve_static(filename):
    return send_from_directory('static', filename)

@app.route('/api/logs', methods=['POST'])
def receive_logs():
    """
    接收来自 BoxLogger 的日志数据并追加到本地 JSON 文件中
    """
    data = request.get_json()
    if not data:
        return jsonify({"status": "error", "message": "Invalid JSON"}), 400
        
    data['server_received_at'] = datetime.now().isoformat()
    
    # 每天生成一个日志文件
    today = datetime.now().strftime("%Y-%m-%d")
    log_file_path = f"data/logs_{today}.jsonl"
    
    with open(log_file_path, "a", encoding="utf-8") as f:
        f.write(json.dumps(data, ensure_ascii=False) + "\n")
        
    return jsonify({"status": "success"})

@app.route('/api/health', methods=['GET'])
def health_check():
    return jsonify({"status": "ok", "message": "SumoController Server is running."})

if __name__ == "__main__":
    print("Starting SumoController Server on http://0.0.0.0:50500")
    # 生产环境中建议使用 gunicorn 或 waitress
    app.run(host="0.0.0.0", port=50500)
