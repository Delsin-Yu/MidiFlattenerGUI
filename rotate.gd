@tool
extends Node2D

@export var degress : float;

func _process(delta: float) -> void:
	rotate(deg_to_rad(degress) * delta);
	if rotation_degrees > 180: rotation_degrees = -180;
