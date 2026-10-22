USE [Practica2];
GO
CREATE OR ALTER PROCEDURE dbo.ConsultarClientes
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdCliente, Cedula, Nombre, Correo, Estado FROM dbo.Clientes ORDER BY Nombre, IdCliente;
END;
GO
CREATE OR ALTER PROCEDURE dbo.RegistrarCliente
    @Cedula varchar(max), @Nombre varchar(max), @Correo varchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SELECT @Cedula = LTRIM(RTRIM(@Cedula)), @Nombre = LTRIM(RTRIM(@Nombre)), @Correo = LTRIM(RTRIM(@Correo));
    IF ISNULL(@Cedula, '') = '' OR LEN(@Cedula) > 50 OR ISNULL(@Nombre, '') = '' OR LEN(@Nombre) > 100
       OR ISNULL(@Correo, '') = '' OR LEN(@Correo) > 100
        THROW 50004, 'Datos de cliente invalidos.', 1;
    BEGIN TRY
        BEGIN TRANSACTION;
        -- Serializa las altas sin modificar el esquema de las tablas del enunciado.
        IF EXISTS (SELECT 1 FROM dbo.Clientes WITH (TABLOCKX, HOLDLOCK)
                   WHERE LTRIM(RTRIM(Cedula)) = @Cedula)
        BEGIN
            -- Rechazo esperado: conserva la regla sin interrumpir el depurador con una excepcion SQL.
            COMMIT TRANSACTION;
            SELECT CAST(-1 AS bigint);
            RETURN;
        END;
        INSERT dbo.Clientes (Cedula, Nombre, Correo, Estado) VALUES (@Cedula, @Nombre, @Correo, 1);
        DECLARE @Id bigint = CONVERT(bigint, SCOPE_IDENTITY());
        COMMIT TRANSACTION;
        SELECT @Id;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.ConsultarMascotas
AS
BEGIN
    SET NOCOUNT ON;
    SELECT m.IdMascota, m.Nombre, m.Especie, m.Raza, m.Peso, m.IdCliente,
           c.Cedula AS CedulaCliente, c.Nombre AS NombreCliente
    FROM dbo.Mascotas m INNER JOIN dbo.Clientes c ON c.IdCliente = m.IdCliente
    ORDER BY m.IdMascota DESC;
END;
GO
CREATE OR ALTER PROCEDURE dbo.RegistrarMascota
    @Nombre varchar(max), @Especie varchar(max), @Raza varchar(max), @Peso decimal(18,6), @IdCliente bigint
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SELECT @Nombre = LTRIM(RTRIM(@Nombre)), @Especie = LTRIM(RTRIM(@Especie)), @Raza = LTRIM(RTRIM(@Raza));
    IF ISNULL(@Nombre, '') = '' OR LEN(@Nombre) > 100 OR ISNULL(@Especie, '') = '' OR LEN(@Especie) > 100
       OR ISNULL(@Raza, '') = '' OR LEN(@Raza) > 100 OR @Peso IS NULL OR @Peso <= 0 OR @Peso > 999999.99
       OR ROUND(@Peso, 2) <> @Peso
        THROW 50004, 'Datos de mascota invalidos.', 1;
    BEGIN TRY
        BEGIN TRANSACTION;
        IF NOT EXISTS (SELECT 1 FROM dbo.Clientes WITH (HOLDLOCK) WHERE IdCliente = @IdCliente)
            THROW 50003, 'El cliente no existe.', 1;
        -- El bloqueo cubre conteo e insercion para impedir altas simultaneas por encima del limite.
        IF (SELECT COUNT_BIG(*) FROM dbo.Mascotas WITH (TABLOCKX, HOLDLOCK)
            WHERE LTRIM(RTRIM(Especie)) COLLATE Latin1_General_100_CI_AS = @Especie COLLATE Latin1_General_100_CI_AS) >= 2
        BEGIN
            -- Rechazo esperado: libera la transaccion local sin lanzar una excepcion SQL.
            COMMIT TRANSACTION;
            SELECT CAST(-2 AS bigint);
            RETURN;
        END;
        INSERT dbo.Mascotas (Nombre, Especie, Raza, Peso, IdCliente) VALUES (@Nombre, @Especie, @Raza, @Peso, @IdCliente);
        DECLARE @Id bigint = CONVERT(bigint, SCOPE_IDENTITY());
        COMMIT TRANSACTION;
        SELECT @Id;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
