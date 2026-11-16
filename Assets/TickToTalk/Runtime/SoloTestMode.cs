using System;
using UnityEngine;

namespace TickToTalk
{
    public sealed partial class NetworkSession
    {
        public bool IsTestMode { get; private set; }
        public bool TestFlying { get; private set; }
        float flightVertical;
        public void StartTestMode()
        {
            if (!net || net.IsListening || net.ShutdownInProgress || OnlineBusy) return;
            ResetRound(); IsTestMode = true; TestFlying = false;
            UsingRelay = false; JoinCode = ""; flightVertical = 0;
            if (wardrobe) looks[0] = wardrobe.Encode(wardrobe.Decode(PlayerPrefs.GetString("TTT_Outfit", "")));
            notices[0] = "일어난 뒤 2초 후 문이 열리고 타이머가 시작됩니다.";
            View = CreateView(0); Status = "오프라인 테스트 모드";
        }
        public void ToggleTestFlight()
        {
            if (!IsTestMode || (Rules.Phase != 1 && Rules.Phase != 4)) return;
            TestFlying = !TestFlying; verticalSpeed[0] = flightVertical = 0; inputs[0] = Vector2.zero;
            if (!TestFlying) heights[0] = TestGroundHeight();
            notices[0] = TestFlying ? "비행 ON · WASD 이동 · Space 상승 · Ctrl 하강 · Shift 가속 · V 종료" : "비행 OFF · 현재 위치에 착지했습니다.";
        }
        public void SetTestVertical(float value)
        {
            if (IsTestMode && TestFlying) flightVertical = float.IsFinite(value) ? Mathf.Clamp(value, -1, 1) : 0;
        }
        float TestGroundHeight()=>GroundHeight(0);
        void MoveTestGround(float delta)=>MoveGround(0,delta,false);
        float GroundHeight(int slot)
        {
            var origin = new Vector3(positions[slot].x, heights[slot] + .15f, positions[slot].y);
            float ground = 0;
            // Find the nearest walkable surface below this location, including tables and the roof.
            foreach (var hit in Physics.RaycastAll(origin, Vector3.down, 100, ~(1 << 31), QueryTriggerInteraction.Ignore))
                if (hit.normal.y > .5f) ground = Mathf.Max(ground, hit.point.y);
            return ground;
        }
        void MoveGround(int slot,float delta,bool constrain)
        {
            if (Now - lastInput[slot] < .3) {
                var movement = new Vector3(inputs[slot].x, 0, inputs[slot].y) * ((sprinting[slot] ? 5.6f : 3.2f) * delta);
                if(constrain){
                    Vector2 next=positions[slot]+new Vector2(movement.x,movement.z);
                    if(!(Rules.Phase==4 && positions[slot].y>31.5f && next.y>31.5f)) next.x=Mathf.Clamp(next.x,slot==0?-7.2f:.6f,slot==0?-.6f:7.2f);
                    next=ConstrainCellMovement(slot,positions[slot],next);
                    movement=new Vector3(next.x-positions[slot].x,0,next.y-positions[slot].y);
                }
                var feet = new Vector3(positions[slot].x, heights[slot], positions[slot].y);
                if (movement.sqrMagnitude > 0 && !Physics.CapsuleCast(feet + Vector3.up * .3f, feet + Vector3.up * 1.4f, .25f, movement.normalized, movement.magnitude, ~(1 << 31), QueryTriggerInteraction.Ignore))
                    positions[slot] += new Vector2(movement.x, movement.z);
            }
            float floor = GroundHeight(slot);
            verticalSpeed[slot] -= 16f * delta;
            heights[slot] = Mathf.Max(floor, heights[slot] + verticalSpeed[slot] * delta);
            if (heights[slot] <= floor) verticalSpeed[slot] = 0;
        }
        void MoveTestFlight(float delta)
        {
            if (Now - lastInput[0] >= .3) return;
            var direction = Vector3.ClampMagnitude(new Vector3(inputs[0].x, flightVertical, inputs[0].y), 1);
            float distance = (sprinting[0] ? 10f : 5f) * delta;
            positions[0] += new Vector2(direction.x, direction.z) * distance;
            positions[0] = new Vector2(Mathf.Clamp(positions[0].x, -100, 100), Mathf.Clamp(positions[0].y, -100, 100));
            heights[0] = Mathf.Clamp(heights[0] + direction.y * distance, 0, 30);
        }
    }
}
