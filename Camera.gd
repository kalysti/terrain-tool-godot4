extends Camera3D

var SENSITIVITY_Y = 0
var INVERSION_MULT = 1
var MAX_Y = 89

func initializeComponents():
	SENSITIVITY_Y = self.get_parent().get_parent().Sensitivity_Y
	MAX_Y = self.get_parent().get_parent().Maximum_Y_Look
	if self.get_parent().get_parent().Invert_Y_Axis:
		INVERSION_MULT = 1
	else:
		INVERSION_MULT = -1

func _ready():
	self.initializeComponents()
	
	pass
	
func _input(event):
	if event is InputEventMouseMotion:
		if INVERSION_MULT * SENSITIVITY_Y * event.relative.y >= 0 and self.rotation_degrees.x >= MAX_Y:
			return
		if INVERSION_MULT * SENSITIVITY_Y * event.relative.y <= 0  and self.rotation_degrees.x <= -MAX_Y:
			return
		rotate_x(INVERSION_MULT * SENSITIVITY_Y * event.relative.y)
