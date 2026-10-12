namespace SELib;

internal enum SEAnim_DataPresenceFlags : byte
{
	SEANIM_BONE_LOC = 1,
	SEANIM_BONE_ROT = 2,
	SEANIM_BONE_SCALE = 4,
	SEANIM_PRESENCE_BONE = 7,
	SEANIM_PRESENCE_NOTE = 64,
	SEANIM_PRESENCE_CUSTOM = 128
}
