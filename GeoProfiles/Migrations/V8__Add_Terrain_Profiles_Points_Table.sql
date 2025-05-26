create table terrain_profile_points
(
    profile_id uuid    not null
        references terrain_profiles (id) on delete cascade,
    seq        int     not null, 
    dist_m     numeric not null,
    elev_m     numeric not null,
    primary key (profile_id, seq)
);
create index ix_tpp_profile_id on terrain_profile_points (profile_id);
