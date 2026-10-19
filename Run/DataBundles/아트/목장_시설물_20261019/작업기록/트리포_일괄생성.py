"""Resume the explicitly authorized eleven ranch jobs; never resubmit an uncertain create."""
import json
import subprocess
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PLAN = ROOT / "작업기록" / "트리포_모델생성_계획.json"
CLI = [r"C:\Program Files\nodejs\node.exe", r"C:\Users\GRAVITY\AppData\Roaming\npm\node_modules\tripo-cli\dist\cli.js"]
LOG = ROOT / "작업기록" / "트리포_진행기록.jsonl"

def record(event, **fields):
    payload = {"time": time.strftime("%Y-%m-%d %H:%M:%S"), "event": event, **fields}
    with LOG.open("a", encoding="utf-8") as stream:
        stream.write(json.dumps(payload, ensure_ascii=False) + "\n")
    print(json.dumps(payload, ensure_ascii=False), flush=True)

def call(*args):
    result = subprocess.run(CLI + list(args) + ["--no-open", "--json"], capture_output=True,
                            text=True, encoding="utf-8", errors="replace", cwd=ROOT, timeout=180)
    messages = []
    for line in result.stdout.splitlines():
        try:
            messages.append(json.loads(line))
        except json.JSONDecodeError:
            pass
    if result.returncode or not messages:
        raise RuntimeError(result.stderr.strip() or result.stdout.strip() or "No CLI result")
    return messages[-1]

def save(plan):
    temporary = PLAN.with_suffix(".tmp")
    temporary.write_text(json.dumps(plan, ensure_ascii=False, indent=2), encoding="utf-8")
    temporary.replace(PLAN)

def main():
    plan = json.loads(PLAN.read_text(encoding="utf-8"))
    balance = call("balance")
    remaining = [asset for asset in plan["assets"] if not asset.get("task_id")]
    if balance["balance"] < 55 * len(remaining):
        raise RuntimeError("Insufficient balance for the planned jobs; no extra purchases are allowed")
    for asset in remaining:
        if asset.get("submission_uncertain"):
            raise RuntimeError("Inspect uncertain submission before retrying: " + asset["id"])
        source = ROOT / "트리포_입력원화" / asset["source"]
        asset["submission_uncertain"] = True
        save(plan)
        args = ["generate", "image-to-model", str(source), "--model", plan["parameters"]["model"],
                "--name", "ranch_" + asset["id"] + "_20261019", "--no-wait"]
        for key, value in {**plan["parameters"], "face_limit": asset["face_limit"]}.items():
            if key != "model":
                args += ["--param", f"{key}={str(value).lower() if isinstance(value, bool) else value}"]
        result = call(*args)
        asset["task_id"] = result["task_id"]
        asset.pop("submission_uncertain", None)
        save(plan)
        record("submitted", id=asset["id"], task_id=asset["task_id"])
        time.sleep(2)
    pending = {a["task_id"]: a for a in plan["assets"]}
    while pending:
        result = call("task", "list", *pending.keys())
        for task in result["tasks"]:
            asset = pending[task["task_id"]]
            state = task.get("status")
            asset["status"] = state
            asset["progress"] = task.get("progress")
            if state == "success":
                output = ROOT / "트리포_생성모델" / Path(asset["source"]).stem
                if not (output / "task.json").exists():
                    call("task", "get", task["task_id"], "--download", "--out", str(output))
                asset["credits_consumed"] = task.get("credits_consumed")
                asset["output_dir"] = str(output)
                record("downloaded", id=asset["id"], credits=asset["credits_consumed"])
                pending.pop(task["task_id"])
            elif state in ("failed", "cancelled", "canceled", "expired"):
                record("failed", id=asset["id"], status=state)
                pending.pop(task["task_id"])
        save(plan)
        if pending:
            record("progress", tasks=[{"id": a["id"], "status": a.get("status"), "progress": a.get("progress")} for a in pending.values()])
            time.sleep(30)
    final_balance = call("balance")
    plan["ending_balance"] = final_balance
    save(plan)
    record("complete", balance=final_balance)

if __name__ == "__main__":
    main()
