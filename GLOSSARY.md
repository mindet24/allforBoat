# TRIPPED!

A puzzle game about a family house where everyone needs power at once. The player works out which socket is on which circuit and keeps every breaker from tripping.

## Structure

**Level**:
One puzzle: its house, its circuits, its requests, its routines and its reference solution.
_Avoid_: Map, stage, แมพ

**House**:
The physical layout of a level: rooms, doors, stairs, sockets and the panel.
_Avoid_: Map, แมพ

**Room**:
One space in the house. Every socket and every request belongs to exactly one room.

**Door**:
A connection between two rooms on the same floor. It can be open or closed.

**Stairs**:
A connection between two rooms on different floors.

**Sight**:
Which rooms the player can currently see.
_Avoid_: Fog, vision range, ระยะสายตา

## Power

**Circuit**:
A group of sockets that share one wire. It has a capacity and a load.
_Avoid_: Breaker (when you mean the wire group)

**Load**:
The sum of the power units of every powered appliance on one circuit.

**Capacity**:
The highest load a circuit can carry without its breaker tripping.

**Breaker**:
The switch that protects one circuit. It is either ON or OFF.
_Avoid_: Circuit (when you mean the switch)

**Trip**:
A breaker turning itself OFF because its circuit stayed over capacity for too long. Breakers trip; circuits do not.

**Main breaker**:
The breaker that protects the whole house. It trips when the total load of all circuits that are ON is too high.

**Panel**:
The box in one room of the house that holds every breaker.
_Avoid_: Fuse box, ตู้ไฟ (in code and docs)

**Socket**:
A place in a room where one appliance can be plugged in. It belongs to one circuit.
_Avoid_: Outlet, plug

**Powered**:
An appliance is powered when it is plugged in, its switch is on, and its circuit's breaker is ON.

## People and things

**Appliance**:
A device that draws load when powered.

**Request**:
A family member's need to run one appliance in one room for a duration, before a deadline.

**Routine**:
A family member's appliance that switches itself on and off at fixed times. The player cannot touch it.
_Avoid_: Event, scheduled event
