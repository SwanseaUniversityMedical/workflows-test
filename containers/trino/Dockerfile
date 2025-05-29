# renovate: datasource=docker depName=trinodb/trino
ARG BASE_VERSION=444

FROM trinodb/trino:$BASE_VERSION
ARG BASE_VERSION
# test!

ARG FOO
ARG BAR
RUN echo "build-args FOO=$FOO, BAR=$BAR"

RUN --mount=type=secret,id=BAZ,env=BAZ --mount=type=secret,id=BIZ,env=BIZ \
    echo "build-secrets BAZ=$BAZ, BIZ=$BIZ"
