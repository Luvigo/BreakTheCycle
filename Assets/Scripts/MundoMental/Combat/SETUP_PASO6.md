# Mundo Mental Oscuro — Paso 6: combate VR (setup en Unity 6.3 + XRI 3.4)

Scripts en: `Assets/Scripts/MundoMental/Combat/`

El template **VRTemplate** `LaunchProjectile.cs` no se modifica; el disparo de energía usa `VRHandPower` + `EnergyProjectile`.

---

## 1) Arquitectura (resumen)

| Pieza | Rol |
|--------|-----|
| `IDamageable` / `IWeapon` | Contratos |
| `DamageInfo` + `DamageSource` | Metadatos de impacto |
| `DamageableUtility` | `GetComponent` sobre interfaces (padres) |
| `HandEquipmentTracker` | Estado: mano der. vacía / pistola / espada |
| `GrabbableWeapon` | Suscrito a `XRGrabInteractable` (solo mano der.) |
| `MundoMentalVrCombat` | Logica y trigger (archivo `MundoMentalVrCombat.cs`) |
| `VRHandPower` | Disparo de energía + prefab de proyectil |
| `EnergyProjectile` | Física + `OnTrigger` + `IDamageable` + choque con escudo |
| `GunWeapon` | Raycast desde boca de fuego + cooldown |
| `SwordWeapon` + `SwordBladeTrigger` | Daño en trigger de la hoja con umbral de velocidad |
| `ShieldController` | Malla/collider + input izquierda (independiente) |
| `EnemyTestDummy` | Vida y logs |

---

## 2) Juego de acciones (Input)

1. En el **Project** busca: `XRI Default Input Actions` (Starter Assets, XRI 3.4.1).  
2. Típicas acciones:  
   - **RightHand > Activate** (gripping/trigger): úsala en **MundoMentalVrCombat → Right Hand Fire**.  
   - **LeftHand > Secondary o Grip**: úsala en **ShieldController → Toggle Or Hold Action** (o la que elijas con coherencia en Quest).

> Si al pulsar el trigger no pasa nada, verifica en el inspector que el **Input Action Reference** apunta a la acción buena (no a UI).

---

## 3) En el `XR Origin (XR Rig)` (raíz del jugador)

1. Añade componente **`HandEquipmentTracker`**.  
2. Asigna **Right Hand Controller Root** = el `Transform` del mando DERECHO (el mismo contenedor cuyo hijo directo tenga el **Direct / Near-Far** interactor).  
3. Asigna **Left Hand Controller Root** = equivalente, mano IZQUIERDA (por futuras ampliaciones).  
4. Añade **`VR Combat (disparo)`** (script `MundoMentalVrCombat`): `Hand Equipment`, `Hand Power`, y **Right Hand Fire** (InputActionReference, RightHand > Activate).  
5. (Opcional) Crea un hijo vacío `Combat_Debug` y no lo uses; no es obligatorio.

---

## 4) Mano DERECHA — muzzle (punto de disparo de energía y pistola)

1. Bajo el **Right Hand Controller** (mismo enlace que usaste en el tracker), crea un vacío **`Right_Muzzle`**.  
2. Posición local aprox. `(0, 0, 0.12)` a `(0, 0, 0.2)` — delante de la mano.  
3. En ese mismo nodo, añade **`VRHandPower`**:  
   - `Projectile Prefab` → (tras crear el prefab, paso 6)  
   - `Muzzle` → el propio `Right_Muzzle`  
   - `Instigator Root` → el **transform raíz** del `XR Origin` o del `Camera Offset` (lo que excluyas con capas)  
4. Vuelve a asignar el **`VRHandPower`** al campo de **`MundoMentalVrCombat`**

---

## 5) Proyectil de energía (prefab)

1. Crea: **3D Object → Sphere**, escala `0.08` o similar, nombre `PF_EnergyProjectile`.  
2. Añade **Rigidbody** (Use Gravity: off; Kinematic: off). **Collider:** Sphere **Is Trigger** = on.  
3. Añade componente **`EnergyProjectile`**.  
4. (Opcional) Crea capa `Projectile` y asígnala; excluye colisión con mano en **Edit > Project Settings > Physics** (matriz) si hace falta.  
5. Arrastra a **Project** para crear el prefab y **bórralo** de la escena.  
6. Asigna el prefab en **`VRHandPower` → Projectile Prefab**

---

## 6) Escudo (mano IZQUIERDA)

1. Bajo el **Left Hand Controller**, vacío `Shield_Root` (para el script).  
2. Hijo: **Quad** o **Cube** aplanado = **Shield_Mesh** (malla visible). Escala aunada; posición al frente de la mano.  
3. Añade **Box Collider** o similar en el mesh, **Is Trigger** = on (para bloquear lógicamente; el proyectil destruye al contacto).  
4. En `Shield_Root` añade **`ShieldController`**:  
   - `Shield Root` = el `Shield_Mesh` (o el padre que tenga collider)  
   - `Shield Trigger` = el collider del escudo  
   - `Toggle Or Hold Action` = acción del mando IZQUIERDO (grip/secondary)  
5. Asegúrate de que el `Shield_Mesh` empiece **inactivo** o que el controlador comience con escudo bajado. El script pone el mesh off al `OnEnable` si toca.

---

## 7) Pistola (mundo o prefab)

1. Crea un **Cube** alargado = cuerpo de pistola, **Sphere** o vacío = **MuzzlePistol** delante.  
2. Añade **Rigidbody**, **Box Collider**, **`XR Grab Interactable`**.  
3. Añade **`GunWeapon`**: muzzle = `MuzzlePistol`.  
4. Añade **`GrabbableWeapon`**:  
   - `Kind` = Pistol  
   - `Pistol` = el `GunWeapon` de este objeto  
   - `Sword` = null  
   - `Tracker` = el del XR Origin (o dejar vacío: busca con `FindFirstObjectByType`)  
   - `Right Hand Root` = mismo transform que usaste en `HandEquipmentTracker` (ej. nodo *Right* del rig) **si** deja el tracker a null en explícito.

Prueba: coger con la **derecha**, disparo con el mismo **trigger** que el energía.

---

## 8) Espada (mundo o prefab)

1. Crea un **Cube** alargado = empuñadura + **hijo** **Cube** delgado = hoja, con **Box Collider** en la hoja: **Is Trigger** = on.  
2. Raíz: **Rigidbody** (no kinématic; masas pequeñas de prueba) + `XR Grab Interactable` + `Box Collider` en el cuerpo (no trigger, para sujetar).  
3. En la **raíz** añade **`SwordWeapon`**.  
4. En el hijo **hoja** añade **`SwordBladeTrigger`**; arrastra referencia a `SwordWeapon` (o se autobusca con parent).  
5. Añade **`GrabbableWeapon`**: `Kind` = Sword, `Sword` = el `SwordWeapon`, Pistol = null.

Corte: mueve el brazo con velocidad > umbral (inspector en `SwordWeapon`).

---

## 9) Enemigo dummy

1. **Capsule** o **Cube** `Enemy_Dummy` delante del jugador, con `Collider` (no trigger).  
2. Añade **`EnemyTestDummy`** y opcional asigna el **MeshRenderer** para *flash* de color.  
3. Asegúrate de que el **`Enemy_Dummy` o un hijo** tenga un **Collider** que el rayo de pistola o el proyectil puedan tocar.

---

## 10) Build profile

1. Incluye esta escena en **File > Build Profiles** (lista de escenas; la tuya en primer lugar).  
2. Comprueba **Android** + OpenXR (Quest) según el proyecto.  

---

## 11) Checklist — Paso 6 “terminado”

- [ ] `HandEquipmentTracker` en XR Origin, raíces de mano asignadas.  
- [ ] `MundoMentalVrCombat` con acción de disparo DERECHO y enlace a `VRHandPower`.  
- [ ] `VRHandPower` con muzzle, prefab `EnergyProjectile` y cooldown aceptable.  
- [ ] `ShieldController` con mesh + collider e input IZQUIERDO.  
- [ ] Pistola: grab + `GunWeapon` + `GrabbableWeapon` (Pistol).  
- [ ] Espada: grab + `SwordWeapon` + `SwordBladeTrigger` (hija) + `GrabbableWeapon` (Sword).  
- [ ] `EnemyTestDummy` recibe daño (raycast y proyectil y espada) y escribe en consola.  
- [ ] **Consola** muestra tags `[MentalVR]` y sub-sistemas `Combat`, `HandEnergy`, `Gun`, `Sword`, `Shield`, `Enemy` según pruebes.  
- [ ] Comportamiento: con **mano vacía** → energía; con **pistola** → bala lógica (rayo); con **espada** → *no* dispara energía, solo corte.  

---

## Problemas frecuentes

- **No log de disparo:** acción de Input no asignada o el **action map** deshabilitado.  
- **Energía se autodestruye al salir:** `Instigator` / exclusión de capa; ajusta matriz de física.  
- **Escudo no para bolas:** el proyectil no es trigger / falta el `ShieldController` en los padres del collider de escudo.  
- **Espada no hace daño:** velocidad baja (sube mín. en `SwordWeapon`) o collider de enemigo en otro hilo.

---

*Namespace: `MundoMental.VR.Combat` — listo para enemigos reales, oleadas y restauración de entorno en pasos futuros.*
