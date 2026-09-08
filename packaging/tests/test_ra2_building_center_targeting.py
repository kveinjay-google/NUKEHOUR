import re
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
WEAPON_DIRECTORY = ROOT / "mods" / "ra2" / "weapons"
TOP_LEVEL = re.compile(r"^([^\s:#][^:]*):(?:\s*#.*)?$")


def weapon_fields():
    weapons = {}
    for path in sorted(WEAPON_DIRECTORY.glob("*.yaml")):
        current = None
        for raw_line in path.read_text(encoding="utf-8").splitlines():
            match = TOP_LEVEL.fullmatch(raw_line)
            if match:
                current = match.group(1)
                if current in weapons:
                    raise AssertionError(f"duplicate weapon `{current}` in {path}")
                weapons[current] = {}
                continue

            if current is None or not raw_line.startswith("\t") or raw_line.startswith("\t\t"):
                continue

            key, separator, value = raw_line[1:].partition(":")
            if separator and (key == "TargetActorCenter" or key.startswith("Inherits")):
                weapons[current][key] = value.strip()

    return weapons


def targets_actor_center(weapons, name, active=()):
    if name in active:
        raise AssertionError(f"inheritance cycle: {' -> '.join((*active, name))}")

    fields = weapons[name]
    if "TargetActorCenter" in fields:
        return fields["TargetActorCenter"].lower() == "true"

    parents = [value for key, value in fields.items() if key.startswith("Inherits")]
    return any(targets_actor_center(weapons, parent, (*active, name)) for parent in parents)


class Ra2BuildingCenterTargetingTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.weapons = weapon_fields()

    def assert_targets_center(self, *weapon_names):
        for name in weapon_names:
            with self.subTest(weapon=name):
                self.assertTrue(
                    targets_actor_center(self.weapons, name),
                    f"{name} must resolve TargetActorCenter: true",
                )

    def test_normal_and_elite_vehicle_shells_target_actor_center(self):
        self.assert_targets_center(
            "105mm", "105mmE", "120mm", "120mmE", "120mmx", "120mmxE"
        )

    def test_normal_and_elite_ifv_missiles_target_actor_center(self):
        self.assert_targets_center("HoverMissile", "HoverMissileE")


if __name__ == "__main__":
    unittest.main()
