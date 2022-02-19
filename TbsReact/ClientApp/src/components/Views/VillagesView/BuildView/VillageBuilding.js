import { Typography } from "@mui/material";
import React, { useState } from "react";
import ContentBox from "../../../ContentBox";
import MUITable from "../../../ref/MUITable";

const VillageBuilding = () => {
	const header = ["Building", "Level"];
	const data = [{ id: 0, building: "Loading ...", level: 0 }];
	const [selected, setSelected] = useState(0);

	const onClick = (vill) => {
		setSelected(vill.id);
	};
	return (
		<>
			<ContentBox>
				<Typography variant="h5">Village Building</Typography>
				<MUITable
					header={header}
					data={data}
					handler={onClick}
					selected={selected}
				/>
			</ContentBox>
		</>
	);
};

export default VillageBuilding;
